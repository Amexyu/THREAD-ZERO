using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreadZero
{
    // v0.7：界面按「明亮底 + 黑色装备块 + 警戒橙」重写。玩法、存档、数值类未改动。
    // 页面绘制分在 GameAppAction / GameAppLoadout / GameAppWardrobe / GameAppModals 四个文件里。
    public sealed partial class GameApp : MonoBehaviour
    {
        private struct Floater { public float value, born, drift; public bool critical, onEnemy; }

        private GameModel model;
        private CombatSession combat;
        private SaveService saves;
        private readonly DollAnimation animationState = new DollAnimation();
        private readonly UiNavigation navigation = new UiNavigation();
        private readonly List<string> recentDrops = new List<string>();
        private readonly HashSet<string> unseen = new HashSet<string>();
        private readonly List<Floater> floaters = new List<Floater>();
        private AudioSource source;
        private AudioClip equipSound, dropSound, rareSound;
        private bool paused, sound = true, lastSaveOk = true;
        private int speed = 1, slotFilter = -1, wardrobeSlot;
        private string selectedItem, selectedLook = "w0", toast = "", toastItem;
        private float clock, saveTimer, toastUntil, toastFrom, dropEffectUntil, dropEffectFrom;
        private float healthShown = 1, enemyShown = 1, previousHit, previousHurt, inventoryScroll;
        private Item lastDrop;
        // v0.8 行进：击败敌人后角色走向下一个敌人，期间战斗逻辑暂停。worldX 是已经走过的世界距离。
        // travelKind：1 赶往下一波，2 首领回收后回到入口，3 街区之间转移。
        private float worldX, travelLeft, travelTotal = 1, travelFrom, travelDistance, wreckWorldX, dropWorldX;
        private int travelKind, wreckDistrict;
        private bool wreckShown, wreckBoss, autoAdvance = true;
        private int packShiftsSeen; private float packShiftAt = -10;
        private float TravelProgress { get { float p = 1 - travelLeft / Mathf.Max(.01f, travelTotal); return p * p * (3 - 2 * p); } }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (Camera.main == null)
            {
                var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor; camera.orthographic = true;
                camera.gameObject.AddComponent<AudioListener>();
            }
            Camera.main.backgroundColor = Ui.Ground;
            saves = new SaveService(Application.persistentDataPath, JsonUtility.FromJson<GameState>, data => JsonUtility.ToJson(data, true), message => Debug.LogWarning(message));
            daggerReview = DaggerReviewRequested();
            model = new GameModel(daggerReview?null:saves.Load());
            if(daggerReview) PrepareDaggerReviewModel();
            combat = new CombatSession(model);
            combat.EnableMovement();
            combat.OnBasicAttack = () => animationState.TriggerAttack(model.IsMelee);
            combat.OnAdvance = dt => animationState.Tick(dt);
            combat.OnHit = () => animationState.TriggerHit();
            combat.OnCheckpoint = Save;
            combat.OnMessage = message => Notify(message, 4);
            combat.OnDrop = (item, fresh) =>
            {
                if (TryAutoSalvage(item, fresh)) { Save(); return; }
                string title = Catalog.QualityNames[(int)item.quality] + (fresh ? " · 新外观" : "");
                Notify(title, (int)item.quality >= 2 ? 5 : 2.5f, item.uid);
                lastDrop = item; dropWorldX = combat.MovementEnabled?combat.Movement.EnemyX:worldX; dropEffectFrom = clock; dropEffectUntil = clock + QualityVisual.DropDuration(item.quality);
                recentDrops.Insert(0, item.uid); if (recentDrops.Count > 5) recentDrops.RemoveAt(5);
                unseen.Add(item.uid);
                Play((int)item.quality >= 2 ? rareSound : dropSound); Save();
            };
            selectedItem = model.state.items.Last().uid;
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
            equipSound = Tone("equip", 620, .065f); dropSound = Tone("drop", 840, .07f); rareSound = Tone("rare", 1060, .18f);
            if (!saves.Writable) Notify("已有存档暂不可读，本次进度不会覆盖原文件。", 10);
            if(daggerReview) BeginDaggerReviewCapture();
        }
        private static AudioClip Tone(string name, float frequency, float duration)
        {
            const int rate = 22050; var data = new float[(int)(rate * duration)];
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(i * 2 * Mathf.PI * frequency / rate) * .12f * (1 - (float)i / data.Length);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private void Play(AudioClip clip) { if (sound && source != null && clip != null) source.PlayOneShot(clip); }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            clock += delta;
            // 战斗按不超过 0.05 秒的小步推进；一旦击败敌人就切到行进，下一只敌人不会在行进途中挨打。
            float budget = paused ? 0 : Mathf.Min(.15f, delta) * speed;
            for (int guard = 0; guard < 64 && budget > .0001f; guard++)
            {
                if (travelLeft > 0)
                {
                    float part = Mathf.Min(travelLeft, budget); budget -= part; travelLeft -= part;
                    animationState.Tick(part);
                    worldX = travelFrom + travelDistance * TravelProgress;
                    if(travelLeft<=0 && combat.MovementEnabled)combat.Movement.ResetAtCamera(worldX);
                    continue;
                }
                float step = Mathf.Min(.05f, budget); budget -= step;
                int wave = combat.wave, bosses = model.state.bosses, region = model.state.region; bool wasBoss = combat.Boss, wasDown = combat.recovery > 0;
                int targetVersion=combat.Movement.TargetVersion;float oldEnemyWorld=combat.Movement.EnemyX;
                combat.Tick(step);
                if(combat.MovementEnabled)
                {
                    worldX=combat.Movement.CameraX;
                    if(targetVersion!=combat.Movement.TargetVersion && !wasDown)
                    {wreckShown=true;wreckWorldX=oldEnemyWorld;wreckDistrict=Catalog.District(region);wreckBoss=wasBoss;}
                }
                if (combat.packShifts != packShiftsSeen) { packShiftsSeen = combat.packShifts; packShiftAt = clock; }
                if (wasDown && combat.recovery <= 0)
                {if(combat.MovementEnabled)combat.Movement.ResetAtCamera(worldX);else BeginTravel(1,region,false,false);}
                else if (model.state.bosses != bosses)
                {
                    // 首领回收：开着自动推进且下一关已解锁就直接前往，否则回到本关入口继续刷。
                    if (autoAdvance && region < model.state.unlockedRegion) ChangeRegion(region + 1, false);
                    else BeginTravel(2, region, true, true);
                }
                else if (combat.wave != wave && !combat.MovementEnabled) BeginTravel(1, region, false, true);
            }
            if (combat.hitFlash > previousHit + .001f) AddFloater(combat.lastPlayerDamage, combat.lastCritical, true);
            if (combat.hurtFlash > previousHurt + .001f) AddFloater(combat.lastEnemyDamage, false, false);
            previousHit = combat.hitFlash; previousHurt = combat.hurtFlash;
            float ease = 1 - Mathf.Exp(-12 * delta);
            healthShown = Mathf.Lerp(healthShown, Mathf.Clamp01(combat.health / Mathf.Max(1, model.Total().health)), ease);
            enemyShown = Mathf.Lerp(enemyShown, Mathf.Clamp01(combat.enemyHealth / Mathf.Max(1, combat.enemyMaxHealth)), ease);
            for (int i = floaters.Count - 1; i >= 0; i--) if (clock - floaters[i].born > .9f) floaters.RemoveAt(i);
            saveTimer += delta; if (saveTimer >= 30) Save();
        }
        private void AddFloater(float value, bool critical, bool onEnemy)
        {
            if (floaters.Count > 12) floaters.RemoveAt(0);
            floaters.Add(new Floater { value = value, born = clock, critical = critical, onEnemy = onEnemy, drift = (floaters.Count % 3 - 1) * 26 });
        }
        private void BeginTravel(int kind, int region, bool boss, bool leaveWreck)
        {
            travelKind = kind; travelTotal = kind == 1 ? 1.1f : kind == 2 ? 1.9f : 2.4f; travelLeft = travelTotal;
            travelFrom = worldX; travelDistance = kind == 1 ? 900 : kind == 2 ? 1700 : 2800;
            wreckShown = leaveWreck; if(!combat.MovementEnabled)wreckWorldX = worldX; wreckDistrict = Catalog.District(region); wreckBoss = boss;
        }
        private void Save() { if (!daggerReview && model != null && saves != null) lastSaveOk = saves.Save(model.state); saveTimer = 0; }
        private void OnApplicationQuit() { Save(); }
        private void OnApplicationPause(bool value) { if (value) Save(); }
        private void Notify(string value, float duration = 3, string item = null)
        { toast = value; toastFrom = clock; toastUntil = clock + duration; toastItem = item; }

        private Item FindItem(string uid) { return string.IsNullOrEmpty(uid) ? null : model.state.items.FirstOrDefault(item => item.uid == uid); }
        private void Go(GameScreen screen)
        {
            if (navigation.Screen == GameScreen.Loadout && screen != GameScreen.Loadout) unseen.Clear();
            navigation.Go(screen);
        }
        private void SelectItem(Item item)
        { if (item == null) return; selectedItem = item.uid; unseen.Remove(item.uid); }
        private void OpenItem(Item item)
        {
            if (item == null) return;
            Go(GameScreen.Loadout); ResetInventoryFilters(); SelectItem(item);
            slotFilter = (int)Catalog.Find(item.baseId).slot; inventoryScroll = 0;
        }
        // show：玩家手动换关时跳回行动页；自动推进时不打断玩家正在看的页面。
        private void ChangeRegion(int region, bool show = true)
        {
            if (!model.ChangeRegion(region)) return;
            combat.ResetRegion(); animationState.Clear(); floaters.Clear(); Save();
            if (show) { navigation.Close(); Go(GameScreen.Action); }
            BeginTravel(3, region, false, false);
        }

        private void OnGUI()
        {
            if (model == null) return;
            Ui.Begin();
            Event current = Event.current;
            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Escape && navigation.HasModal) { navigation.Close(); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Space) { paused = !paused; current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Alpha1) { Go(GameScreen.Action); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Alpha2) { Go(GameScreen.Loadout); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Alpha3) { Go(GameScreen.Wardrobe); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Alpha4) { Go(GameScreen.Workshop); current.Use(); }
            }
            Ui.FillScreen(Ui.Ground);
            bool modal = navigation.HasModal;
            Ui.Blocked = modal;
            switch (navigation.Screen)
            {
                case GameScreen.Action: ActionPage(); break;
                case GameScreen.Loadout: LoadoutPage(); break;
                case GameScreen.Wardrobe: WardrobePage(); break;
                case GameScreen.Workshop: WorkshopPage(); break;
            }
            Rail(); Toast();
            Ui.Blocked = false;
            if (modal && navigation.HasModal) Modal();
        }

        private void Rail()
        {
            Ui.Fill(0, 0, 96, 900, Ui.Ink);
            Ui.Fill(20, 20, 56, 56, Ui.Orange);
            Ui.Text(20, 20, 56, 56, "TZ", 30, Ui.Ink, TextAnchor.MiddleCenter, true, true);
            string[] names = { "行动", "整备", "衣柜", "工坊" };
            for (int i = 0; i < 4; i++)
            {
                float y = 112 + i * 92; bool active = (int)navigation.Screen == i, hover = Ui.Hover(0, y, 96, 84);
                Color background = active ? Ui.Panel : hover ? Ui.Tile2 : Ui.Ink, color = active ? Ui.Ink : hover ? Ui.Panel : Ui.MuteOnInk;
                Ui.Fill(0, y, 96, 84, background);
                if (i == 0) Ui.IconAction(48, y + 30, color, background);
                else if (i == 1) Ui.IconGrid(48, y + 30, color);
                else if (i == 2) Ui.IconHanger(48, y + 30, color, background);
                else Ui.IconWrench(48, y + 30, color, background);
                Ui.Text(0, y + 50, 96, 24, names[i], 14, color, TextAnchor.MiddleCenter, active);
                if (i == 1 && unseen.Count > 0)
                {
                    Ui.Fill(60, y + 8, 26, 20, Ui.Orange);
                    Ui.Text(60, y + 8, 26, 20, Math.Min(99, unseen.Count).ToString(), 14, Ui.Ink, TextAnchor.MiddleCenter, true, true);
                }
                if (Ui.Click(0, y, 96, 84)) Go((GameScreen)i);
            }
            bool over = Ui.Hover(20, 824, 56, 56);
            Ui.Fill(20, 824, 56, 56, over ? Ui.Tile2 : Ui.Ink); Ui.Frame(20, 824, 56, 56, 1, over ? Ui.Panel : Ui.Steel);
            Ui.IconGear(48, 852, over ? Ui.Panel : Ui.MuteOnInk, over ? Ui.Tile2 : Ui.Ink);
            if (Ui.Click(20, 824, 56, 56)) navigation.Open(GameModal.Settings);
        }

        private void Toast()
        {
            if (clock >= toastUntil || string.IsNullOrEmpty(toast)) return;
            float slide = Mathf.Clamp01((clock - toastFrom) / .18f), y = (navigation.Screen == GameScreen.Action ? 128 : 812) - (1 - slide) * 14;
            Item item = FindItem(toastItem);
            if (item != null)
            {
                EquipmentBase b = Catalog.Find(item.baseId); Color quality = QualityVisual.Colors[(int)item.quality];
                float width = Mathf.Max(Ui.Width(b.name, 17, true), Ui.Width(toast, 13, true)) + 190, x = 848 - width / 2;
                Ui.Fill(x, y, width, 64, Ui.Ink);
                Ui.ItemTile(x + 8, y + 8, 48, 48, b, item.quality, true, false, 3, false);
                Ui.Text(x + 70, y + 9, width - 170, 20, toast, 13, quality, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 70, y + 31, width - 170, 24, b.name, 17, Ui.Panel, TextAnchor.MiddleLeft, true);
                if (Ui.Button(x + width - 96, y + 8, 88, 48, "查看", 3, true, 15)) OpenItem(item);
            }
            else
            {
                float width = Mathf.Min(760, Ui.Width(toast, 15, true) + 56), x = 848 - width / 2;
                Ui.Fill(x, y, width, 52, Ui.Ink); Ui.Fill(x, y, 6, 52, Ui.Orange);
                Ui.Text(x + 24, y, width - 40, 52, toast, 15, Ui.Panel, TextAnchor.MiddleLeft, true);
            }
        }

        // 整备和衣柜里的角色预览：按外套外观显示整帧，持近战武器外观时用持刃站姿。缺图时退回旧绘制。
        private void PreviewHero(float feetX, float feetY, float canvas, float oldX, float oldY, float oldW, float oldH)
        {
            if(DaggerActorView.Draw(model,feetX,feetY,canvas,0,Color.white))return;
            bool blade = model.Appearance(Slot.Weapon).attackStyle == AttackStyle.Melee;
            string headId = model.Equipped(Slot.Head) == null && model.state.appearance[(int)Slot.Head] == null
                ? "_default" : model.Appearance(Slot.Head).id;
            if (!SkeletalActorView.Draw(feetX, feetY, canvas, model.Appearance(Slot.Coat).style,
                headId, model.Appearance(Slot.Weapon).id, model.Appearance(Slot.Back).id,
                ActorView.IdleFrame(blade), false, 0, 0, Color.white)
                && !ActorView.Player(feetX, feetY, canvas, model.Appearance(Slot.Coat).style, ActorView.IdleFrame(blade), Color.white, headId,
                model.Appearance(Slot.Weapon).id))
                CharacterView.Draw(model, Ui.R(oldX, oldY, oldW, oldH), clock);
        }

        private static float[] Values(Stats s) { return new[] { s.health, s.attack, s.defense, s.haste, s.crit, s.luck }; }
        private static string Value(int stat, float value) { return stat == 3 || stat == 4 ? (value * 100).ToString("F1") + "%" : value.ToString("F1"); }
        private static void SplitEffect(Effect effect, out string name, out string description)
        {
            string full = Catalog.EffectName(effect); int cut = full.IndexOf('：');
            if (effect == Effect.None || cut < 0) { name = "无固定效果"; description = "这件装备只提供属性。"; }
            else { name = full.Substring(0, cut); description = full.Substring(cut + 1); }
        }
    }
}
