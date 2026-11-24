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
        private struct Floater { public float value, born, drift, lift, x; public bool critical, onEnemy; }

        private GameModel model;
        private CombatSession combat;
        private SaveService saves;
        private readonly DollAnimation animationState = new DollAnimation();
        private readonly UiNavigation navigation = new UiNavigation();
        private readonly List<string> recentDrops = new List<string>();
        private readonly HashSet<string> unseen = new HashSet<string>();
        private readonly List<Floater> floaters = new List<Floater>();
        private AudioSource source;
        private AudioClip equipSound, dropSound, rareSound, salvageSound, bossSound;   // v0.20 企划要求分解、首领出现也有各自的提示音
        private bool bossWas;
        private bool paused, lastSaveOk = true;
        private float slowMo = 1;   // 测试用慢放（桥接脚本 set slowMo 0.2 录技能动作）
        private int speed = 1, slotFilter = -1, wardrobeSlot, wardrobeChapter = 1, mapChapter, setCheckCount = -1;
        private bool wardrobeSets;   // v0.21 衣柜的「套装」视图
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
        // v0.14 关卡转场：transitionLeft > 0 时战斗暂停，播放约 2 秒的转场，中点切换关卡。
        private float transitionLeft, clearedFor, transitionDur = 1, stageTagAt = -10; private int transitionFrom, transitionTo, transitionKind; private bool transitionSwapped; private string stageTagNote = "";
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
            // F8 切换正式存档 / 测试存档（测试进度存在另一个文件，不碰正式存档）；F9 清空测试存档重开。
            DetectExhibit();   // v0.21 展览模式：单独的展览存档，每次启动从起点开始
            testSave = !ExhibitMode && PlayerPrefs.GetInt(TestSaveKey, 0) == 1;
            saves = new SaveService(Application.persistentDataPath, JsonUtility.FromJson<GameState>, data => JsonUtility.ToJson(data, true), message => Debug.LogWarning(message),
                ExhibitMode ? ExhibitSaveName : testSave ? "thread-zero-save-test.json" : "thread-zero-save.json");
            daggerReview = DaggerReviewRequested();
            GameState loaded = daggerReview ? null : saves.Load();
            model = new GameModel(loaded);
            if (loaded != null) MarkPassedUnlockTips();
            if(daggerReview) PrepareDaggerReviewModel();
            combat = new CombatSession(model);
            combat.EnableMovement();
            combat.OnBasicAttack = () => animationState.TriggerAttack(model.IsMelee);
            combat.OnAdvance = dt => animationState.Tick(dt);
            combat.OnHit = () => { animationState.TriggerHit(); Sfx("hurt", .45f, .25f); };
            combat.OnEnemyAct = OnEnemyAct;
            combat.OnFloorCleared = (floor, best) => { if (best && floor % 10 == 0 && model.state.anomalies.Count < Chapters.MaxAnomalies) OfferAnomalies(); };
            combat.OnSkillCast = OnSkillCast; combat.OnSkillHit = OnSkillHit; combat.OnAbility = OnAbilityFx;
            combat.OnRobotShot = OnRobotShot; combat.OnRobotSupport = OnRobotSupport;   // v0.21 机库
            combat.OnChainPull = e => EnemyFx.Burst(clock, e.x, GroundY - e.canvas * .4f, new Color(1, .62f, .25f), 1.1f);   // 钩拉：被拽的怪身上闪一下
            combat.OnCoilJump = CoilJumpFx;   // 线圈普攻：电流跳到第二只怪
            combat.OnImplant = d => { implantShown = d; implantAt = clock; implantNew = d.id; implantNewSeenAt = -1; Play(rareSound); Save(); };
            combat.OnDamage = (value, critical, x) => { AddFloater(value, critical, true, x); GearHit(critical, x); Sfx(critical ? "hit_crit" : model.IsMelee ? "hit_melee" : "hit", critical ? .5f : .35f, .07f); };
            combat.OnCheckpoint = Save;
            combat.OnMessage = message => Notify(message, 4);
            combat.OnDrop = (item, fresh) =>
            {
                if (TryAutoSalvage(item, fresh)) { Save(); return; }
                // v0.16 去掉顶部「掉落了什么 · 查看」提示：掉落本身已经有弹出 → 吸入角色的动画，新物品在背包里有「新」标记。
                lastDrop = item; SpawnDrop(item);
                if ((int)item.quality >= (int)Quality.Legendary)
                {   // 11-23 机密、原型掉落：停一下、震一下，底下出一条带「查看」的提示
                    Impact.Stop(.06f); Impact.Shake(clock, (int)item.quality >= 5 ? 7 : 4, .28f); Sfx("unlock", .6f, .3f);
                    Notify("掉落 · " + Catalog.QualityNames[(int)item.quality], 5, item.uid);
                }
                recentDrops.Insert(0, item.uid); if (recentDrops.Count > 5) recentDrops.RemoveAt(5);
                unseen.Add(item.uid); QueueTip(Tip.Drop);
                Play((int)item.quality >= 2 ? rareSound : dropSound); Save();
            };
            selectedItem = model.state.items.Last().uid;
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
            SetupAudio();   // v0.21 音乐与音效（GameAppAudio.cs）
            equipSound = Tone("equip", 620, .065f); dropSound = Tone("drop", 840, .07f); rareSound = Tone("rare", 1060, .18f);
            salvageSound = Tones("salvage", new[] { 700f, 470f }, .05f, .015f); bossSound = Tones("boss", new[] { 196f, 196f, 147f }, .11f, .05f);
            if (!saves.Writable) Notify("已有存档暂不可读，本次进度不会覆盖原文件。", 10);
            if(daggerReview) BeginDaggerReviewCapture();
            else { SetupTitle(loaded != null); if (!TitleBlocking) ApplyOffline(); }   // v0.21 先进标题画面，离线收益等离开标题再算
        }

        // v0.20 离线收益：上次存档到现在过了多久，按挂机时的零件收入 × 25% 补给零件（最多算 2 小时），弹「欢迎回来」。
        private double offlineAway; private float offlineCounted; private int offlineGain;
        /// 测试用（桥接 call TestOffline 180）：假装离开了这么多分钟。
        public void TestOffline(int minutes) { model.state.savedAt = System.DateTime.UtcNow.AddMinutes(-minutes).Ticks; ApplyOffline(); }
        public void ApplyOffline()
        {
            long now = System.DateTime.UtcNow.Ticks, then = model.state.savedAt; model.state.savedAt = now;
            if (ExhibitMode) return;   // v0.21 展览模式不算离线收益
            if (then <= 0 || now <= then) return;
            double away = (now - then) / 1e7; if (away < Balance.OfflineMinSeconds) return;
            float counted = (float)System.Math.Min(away, model.OfflineMax);   // v0.21 改造板「拾荒」：上限 4 小时
            int gain = Mathf.RoundToInt(model.state.scrapRate * counted * Balance.OfflineEfficiency);
            if (gain <= 0) return;
            model.state.scrap += gain; offlineAway = away; offlineCounted = counted; offlineGain = gain;
            navigation.Open(GameModal.Offline); Save();
        }
        private static AudioClip Tone(string name, float frequency, float duration)
        {
            const int rate = 22050; var data = new float[(int)(rate * duration)];
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(i * 2 * Mathf.PI * frequency / rate) * .12f * (1 - (float)i / data.Length);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        /// 几个音连着（每个音线性衰减，中间留一点空），和 Tone 一样是程序生成的短音（企划：原型不依赖来源不明的音频）。
        private static AudioClip Tones(string name, float[] frequencies, float note, float gap)
        {
            const int rate = 22050; int per = (int)(rate * note), pause = (int)(rate * gap);
            var data = new float[frequencies.Length * (per + pause)];
            for (int n = 0; n < frequencies.Length; n++)
                for (int i = 0; i < per; i++) data[n * (per + pause) + i] = Mathf.Sin(i * 2 * Mathf.PI * frequencies[n] / rate) * .12f * (1 - (float)i / per);
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            clock += delta * slowMo;   // 慢放录像时画面特效也一起慢下来
            if (!model.state.chipGift && model.GiftChips())   // v0.21 第一次到第五章：送一块芯片、一件改装件，让玩家知道有这个系统
            { Save(); Notify("芯片与改装件开放了：送你一块「过载 Mk1」和一件「标记」改装件，去工坊的装备台装上。", 7); }
            if (model.state.collection.Count != setCheckCount)   // v0.21 图鉴奖励：收藏数变了才检查套装（读档后第一帧也会补发以前集齐的）
            {
                setCheckCount = model.state.collection.Count;
                List<int> sets = model.ClaimSets();
                if (sets.Count == 1) Notify("集齐套装「" + Catalog.SetNames[sets[0]] + "」：核心 +" + Balance.SetCores + "、织网碎片 +" + Balance.SetShards, 6);
                else if (sets.Count > 1) Notify("集齐 " + sets.Count + " 套套装：核心 +" + Balance.SetCores * sets.Count + "、织网碎片 +" + Balance.SetShards * sets.Count, 6);
                foreach (int ch in sets.Select(k => k / 5 + 1).Distinct())
                    if (model.TitleUnlocked(ch)) Notify("集齐第" + Catalog.ChapterNumber(ch) + "章全部 5 套：解锁称号「" + Catalog.TitleNames[ch - 1] + "」（衣柜 · 套装里可以换）", 7);
                if (sets.Count > 0) { Save(); Play(rareSound); }
            }
            if (!model.state.hangarGift && model.GiftHangar())   // v0.21 打完第一章首领：机库开放，回收无人机成为第一台支援机
            { combat.RobotsChanged(); Save(); Notify("机库开放了：回收无人机成了第一台支援机，每 8 秒给你回血。打怪会掉机器人的图纸碎片，集满 10 片去工坊的机库组装。", 7); }
            bool bossNow = combat != null && combat.BossEnemy != null;   // v0.20 首领出场：低沉的两短一长提示音
            if (bossNow && !bossWas) Play(bossSound);
            bossWas = bossNow;
            // 战斗按不超过 0.05 秒的小步推进；一旦击败敌人就切到行进，下一只敌人不会在行进途中挨打。
            ExhibitTick();
            AudioTick(delta);   // v0.21 按场景换曲、交叉淡化
            float budget = paused || TitleBlocking ? 0 : Mathf.Min(.15f, delta) * speed * slowMo;   // v0.21 标题和开场期间战斗暂停
            for (int guard = 0; guard < 64 && budget > .0001f; guard++)
            {
                budget = Impact.Absorb(budget);   // v0.20 顿帧：重击命中后战斗停一小会儿
                if (budget <= .0001f) break;
                if (transitionLeft > 0)
                {
                    float part = Mathf.Min(transitionLeft, budget); budget -= part; transitionLeft -= part;
                    animationState.Tick(part);
                    if (!transitionSwapped && transitionLeft <= transitionDur * (1 - TransitionSwapAt(transitionKind)) + .0001f)
                    {
                        transitionSwapped = true;
                        if (IsEcho(transitionTo)) model.state.echoFloor = transitionTo - EchoCode;   // 主线进度（region）保持不变，离开回声层时回到原来的章节
                        else { model.state.echoFloor = 0; model.ChangeRegion(transitionTo); }
                        combat.ResetRegion(); animationState.Clear(); floaters.Clear(); Save();
                        stageTagAt = clock + (transitionKind == TransGate ? transitionDur * .7f : transitionDur * .6f);
                    }
                    continue;
                }
                float step = Mathf.Min(.05f, budget); budget -= step;
                int region = model.state.region;
                combat.Tick(step);
                worldX = combat.Movement.CameraX;
                if (combat.packShifts != packShiftsSeen) { packShiftsSeen = combat.packShifts; packShiftAt = clock; }
                if (combat.RetreatRequested)
                {
                    // 被打倒：撤回上一关刷（开着自动推进时，过关后会自动再挑战）。
                    combat.RetreatRequested = false;
                    if (model.InEcho) BeginEchoTransition(Mathf.Max(1, model.state.echoFloor - 1));   // 回声层：退 1 层继续刷（v0.22，和主线一致）
                    else { StuckHint(); QueueTip(Tip.Defeat); BeginStageTransition(model.RetreatTarget()); }   // 不跨章；在回刷的关倒下就重打这一关
                }
                else if (combat.StageCleared)
                {
                    // 过关：先让角色往前走一小段，再转场。开着自动推进就去下一关，否则重刷本关。
                    clearedFor += step;
                    if (clearedFor >= .5f)
                    {
                        if (model.InEcho) BeginEchoTransition(autoAdvance ? model.state.echoFloor + 1 : model.state.echoFloor);
                        else if (region == Catalog.LastRegion && autoAdvance && model.EchoUnlocked) EnterEcho();
                        else BeginStageTransition(autoAdvance && region < model.state.unlockedRegion && model.CanAdvanceAfterClear() ? region + 1 : region);   // 墙前回刷：变强了才往前
                    }
                }
            }
            if (combat.hurtFlash > previousHurt + .001f) AddFloater(combat.lastEnemyDamage, false, false, combat.Movement.HeroX);
            previousHit = combat.hitFlash; previousHurt = combat.hurtFlash;
            float ease = 1 - Mathf.Exp(-12 * delta);
            healthShown = Mathf.Lerp(healthShown, Mathf.Clamp01(combat.health / Mathf.Max(1, model.Total().health)), ease);
            enemyShown = Mathf.Lerp(enemyShown, Mathf.Clamp01(combat.enemyHealth / Mathf.Max(1, combat.enemyMaxHealth)), ease);
            for (int i = floaters.Count - 1; i >= 0; i--) if (clock - floaters[i].born > .9f) floaters.RemoveAt(i);
            saveTimer += delta; if (saveTimer >= 30) Save();
        }
        private void AddFloater(float value, bool critical, bool onEnemy, float x)
        {
            if (floaters.Count > 16) floaters.RemoveAt(0);
            // 群怪时一帧里会有很多伤害：横向随机散开、纵向错层，避免数字叠成一团。
            floaters.Add(new Floater { value = value, born = clock, critical = critical, onEnemy = onEnemy, x = x, drift = UnityEngine.Random.Range(-46f, 46f), lift = UnityEngine.Random.Range(0f, 56f) });
        }
        // 测试用：F4 依次预览三种转场（无缝推进 → 快闸 → 织线一扫），尽量换到另一个场景的关，方便看出前后差别。
        private int previewKind;
        private void PreviewTransition()
        {
            if (transitionLeft > 0) return;
            int from = model.state.region, to = from, scene = Chapters.Stage(from).backdrop;
            for (int r = 0; r <= model.state.unlockedRegion; r++)
                if (Chapters.Stage(r).backdrop != scene && Chapters.Stage(r).backdrop % 4 < 3) { to = r; break; }
            transitionFrom = from; transitionTo = to; transitionKind = previewKind;
            string[] names = { "无缝推进", "快闸", "织线一扫" };
            stageTagNote = "预览 · " + names[previewKind];
            transitionDur = TransitionLength(transitionKind); transitionLeft = transitionDur; transitionSwapped = false; clearedFor = 0;
            Notify("转场预览：" + names[previewKind] + (to == from ? "（只解锁了一种场景，按 F5 解锁全部关卡看得更清楚）" : ""), 2.5f);
            previewKind = (previewKind + 1) % 3;
        }
        private void BeginStageTransition(int to)
        {
            if (transitionLeft > 0) return;
            transitionFrom = CurrentCode; transitionTo = Mathf.Clamp(to, 0, model.state.unlockedRegion);
            StageDef next = Chapters.Stage(transitionTo);
            // 进精英 / 首领关用快闸；回到一章开头（第一次进本章）用织线一扫；其余（过关、撤回、重刷）无缝推进
            transitionKind = next.kind != StageKind.Normal && transitionTo != transitionFrom ? TransGate
                : transitionTo % Chapters.StagesPerChapter == 0 && transitionFrom != transitionTo ? TransWeave : TransPush;
            stageTagNote = IsEcho(transitionFrom) ? "离开零号深网" : transitionTo < transitionFrom ? "撤回补给" : transitionTo == transitionFrom ? "重刷本关" : "";
            transitionDur = TransitionLength(transitionKind);
            transitionLeft = transitionDur; transitionSwapped = false; clearedFor = 0;
        }
        // 回声层换层。进入回声层用织线一扫；精英 / 首领层用快闸；其余无缝推进。
        /// 进回声层：从 EchoStartFloor 开始，跳过的里程碑补发核心。
        private void EnterEcho()
        {
            if (transitionLeft > 0 || !model.EchoUnlocked) return;
            int start = model.EchoStartFloor(), got = model.CatchUpEcho(start);
            if (got > 0) Notify("零号深网从第 " + start + " 层开始 · 跳过的里程碑补发核心 +" + got, 4);
            BeginEchoTransition(start);
        }
        private void BeginEchoTransition(int floor)
        {
            if (transitionLeft > 0 || !model.EchoUnlocked) return;
            floor = Mathf.Max(1, floor);
            int cur = model.InEcho ? model.state.echoFloor : 0;
            transitionFrom = CurrentCode; transitionTo = EchoCode + floor;
            StageKind kind = Chapters.EchoKind(floor);
            transitionKind = !IsEcho(transitionFrom) ? TransWeave : kind != StageKind.Normal && floor != cur ? TransGate : TransPush;
            stageTagNote = !IsEcho(transitionFrom) ? "进入零号深网" : floor < cur ? "退回 " + (cur - floor) + " 层" : floor == cur ? "重刷本层" : "";
            transitionDur = TransitionLength(transitionKind);
            transitionLeft = transitionDur; transitionSwapped = false; clearedFor = 0;
        }
        // 关卡编号：主线 0..6；回声层用 EchoCode + 层数。
        public const int EchoCode = 1000;
        private int CurrentCode { get { return model.InEcho ? EchoCode + model.state.echoFloor : model.state.region; } }
        private static bool IsEcho(int code) { return code >= EchoCode; }
        private static int SceneOf(int code) { return IsEcho(code) ? Chapters.EchoScene(code - EchoCode) : Chapters.Stage(code).backdrop; }
        private static StageKind KindOf(int code) { return IsEcho(code) ? Chapters.EchoKind(code - EchoCode) : Chapters.Stage(code).kind; }
        private static string CodeLabel(int code) { return IsEcho(code) ? "E" + (code - EchoCode) : Catalog.StageCode(code); }
        private static string NameLabel(int code) { return IsEcho(code) ? "零号深网 · 第 " + (code - EchoCode) + " 层" : Catalog.RegionNames[code]; }
        private static string LineOf(int code) { return IsEcho(code) ? "织网用记忆重建的楼层，越往上越不稳定" : code < Catalog.StageLines.Length ? Catalog.StageLines[code] : ""; }
        private void BeginTravel(int kind, int region, bool boss, bool leaveWreck)
        {
            travelKind = kind; travelTotal = kind == 1 ? 1.1f : kind == 2 ? 1.9f : 2.4f; travelLeft = travelTotal;
            travelFrom = worldX; travelDistance = kind == 1 ? 900 : kind == 2 ? 1700 : 2800;
            wreckShown = leaveWreck; if(!combat.MovementEnabled)wreckWorldX = worldX; wreckDistrict = Catalog.District(region); wreckBoss = boss;
        }
        private void Save() { if (!daggerReview && !restarting && model != null && saves != null) { model.state.savedAt = System.DateTime.UtcNow.Ticks; lastSaveOk = saves.Save(model.state); } saveTimer = 0; }
        private void OnApplicationQuit() { Save(); Loc.DumpMissing(); }
        /// 测试用：切语言（0 中文 1 日文 2 英文 3 韩文）；DumpLoc 把这次没翻到的句子写进 Logs/Loc/。
        public void TestLang(int i) { Loc.Set((Loc.Lang)i); }
        public void DumpLoc() { Loc.DumpMissing(); }

        // ---------- 测试存档 ----------
        private const string TestSaveKey = "tz_test_save";
        private bool testSave, restarting;
        private void SwitchSaveSlot()
        {
            Save();
            PlayerPrefs.SetInt(TestSaveKey, testSave ? 0 : 1); PlayerPrefs.Save();
            RestartGame();
        }
        private void RestartGame()
        {
            GameModel.TestPower = false;
            restarting = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
        // 右上角常驻小标签，提醒现在用的是测试存档。
        private void TestSaveBadge()
        {
            const string label = "测试存档 · F8 切回正式 · F9 清空重开";
            float w = Mathf.Min(520, Ui.Width(label, 13, true) + 28);   // 11-23 宽度跟着文字走：英文 / 日文比 300 长，原来伸出屏幕右边
            Ui.Fill(1600 - w - 16, 8, w, 26, Ui.Alpha(Ui.Orange, .92f));
            Ui.FitText(1600 - w - 16, 8, w, 26, label, 13, Ui.OnAccent, TextAnchor.MiddleCenter, true);
        }
        private void OnApplicationPause(bool value) { if (value) Save(); }
        // 测试用：F2 依次换上第一、二章的整套外观（不用等掉落）：h0c1 → A → B → C → D → 循环。
        private int testOutfit;
        private static readonly string[][] TestOutfits = {
            new[] { "h0", "c1", "b0", "w1", "l1" }, new[] { "h1", "c2", "b1", "w0", "l2" }, new[] { "h2", "c3", "b2", "w2", "l3" },
            new[] { "h3", "c4", "b3", "w3", "l4" }, new[] { "h4", "c2", "b4", "w4", "l2" },
            // 第二章 5 套
            new[] { "h5", "c5", "b5", "w5", "l5" }, new[] { "h6", "c6", "b6", "w6", "l6" }, new[] { "h7", "c7", "b7", "w7", "l7" },
            new[] { "h8", "c8", "b8", "w8", "l8" }, new[] { "h9", "c9", "b9", "w9", "l9" } };
        /// 测试（桥接脚本用）：给一件第五章史诗武器、5000 零件，在工坊里选中、锁住第一条词条，用来看洗炼界面。
        private void TestReforgeItem()
        {
            Item item = model.Create("w22", Quality.Epic, 80); model.state.items.Add(item); model.Unlock("w22");
            model.state.scrap += 5000; model.ToggleLock(item, 0); selectedItem = item.uid;
        }
        private void TestReforgeOnce() { if (selectedItem != null) model.Reforge(selectedItem); }
        /// 测试（桥接脚本用）：换上某把武器（录技能动作 GIF 用）。
        /// 测试（桥接脚本用）：往背包里放一件指定等级的装备（不穿）。
        private void TestAddItem(string id, int level) { Item item = model.Create(id, Quality.Rare, level); model.state.items.Add(item); model.Unlock(id); }
        private void TestEquip(string id) { Item item = model.Create(id, Quality.Rare, 30); model.state.items.Add(item); model.Unlock(id); model.Equip(item.uid); }
        /// 测试用：五个部位都换成同款、指定品质和等级的装备（看装备光效）。没穿的部位用初始装备那一款。
        /// 测试：掉一件指定品质的装备（走正式的掉落流程：光柱、停顿、提示）。
        public void TestDrop(int q) { Item it = model.Create("h" + (model.Chapter - 1) * 5, (Quality)Mathf.Clamp(q, 0, 6), 20); model.state.items.Add(it); model.Unlock(it.baseId); combat.OnDrop(it, true); }
        /// 测试：选中身上某个部位的装备（整备页 / 工坊右边显示它）。
        public void TestSelect(int slot) { Item it = model.Equipped((Slot)slot); if (it != null) selectedItem = it.uid; }
        /// 测试：往仓库里塞一批各品质的装备，看卡片分档和排版。
        public void TestBag(int n)
        {
            string[] ids = { "w3", "h4", "c8", "b1", "l7", "w17", "h18", "c23", "b20", "l24" };
            for (int i = 0; i < n; i++) { Item it = model.Create(ids[i % ids.Length], (Quality)(i % 7), 20 + i % 9); model.state.items.Add(it); model.Unlock(it.baseId); }
            model.state.scrap = 123456;
        }
        private void TestGearQuality(int q, int level)
        {
            for (int s = 0; s < 5; s++)
            {
                Item cur = model.Equipped((Slot)s); string id = cur != null ? cur.baseId : Catalog.StarterIds[s];
                Item item = model.Create(id, (Quality)Mathf.Clamp(q, 0, 6), level); model.state.items.Add(item); model.Unlock(id); model.Equip(item.uid);
            }
        }

        /// v0.20 开发用：F7 把飞升度跳到下一个阶段（10% → 30% → 60% → 90% → 100% → 回到 10%），看各阶段的外观。
        /// 五个义体都装上，等级从第一个开始往上填；只在测试存档里能用（按 F8 切换），正式进度不动。
        private void TestNextAscension()
        {
            if (!testSave) { Notify("F7 只在测试存档里能用（先按 F8 切到测试存档）", 2.5f); return; }
            foreach (ImplantDef d in Catalog.Implants) if (!model.state.implants.Contains(d.id)) model.state.implants.Add(d.id);
            int stage = model.AscensionStage, n = Catalog.Implants.Length;
            int pct = stage >= Catalog.AscensionStageAt.Length ? 10 : Catalog.AscensionStageAt[stage];
            int rest = Mathf.RoundToInt(pct / 100f * n * Catalog.ImplantMaxLevel) - n;   // 每个至少 1 级，剩下的从第一个往上填
            for (int i = 0; i < n && i < model.state.implantLevels.Length; i++)
            {
                int add = Mathf.Clamp(rest, 0, Catalog.ImplantMaxLevel - 1);
                model.state.implantLevels[i] = 1 + add; rest -= add;
            }
            int now = model.AscensionStage;
            if (now > 0) { stageShown = now; stagePending = true; }   // 回到行动页时放这一阶段的横幅（100% 的横幅放完会接着弹尾声）
            Notify("测试：机械飞升 " + Mathf.RoundToInt(model.Ascension * 100) + "%" + (now > 0 ? "（" + Catalog.AscensionStageNames[now - 1] + "）" : "") + "，再按 F7 下一阶段", 3);
            Save();
        }

        /// v0.22 开发用：F10 给测试存档全套装备（每种一件、90 级），看光效和外观。再按一次换下一档品质：
        /// 零号 → 原型 → 机密 → 军规 → 特装 → 改良 → 量产 → 回到零号。同时放开全部章节、衣柜全解锁、给够零件和核心。只在测试存档里能用。
        private int allGearQuality = -1;
        private void GrantAllGear()
        {
            if (!testSave) { Notify("F10 只在测试存档里能用（先按 F8 切到测试存档）", 2.5f); return; }
            allGearQuality = allGearQuality <= 0 ? 6 : allGearQuality - 1;
            Quality q = (Quality)allGearQuality;
            string[] worn = new string[Catalog.SlotCount];
            for (int i = 0; i < Catalog.SlotCount; i++) { Item it = model.Equipped((Slot)i); worn[i] = it != null ? it.baseId : Catalog.StarterIds[i]; }
            model.state.items.Clear();
            foreach (EquipmentBase b in Catalog.All)
            {
                Item item = model.Create(b.id, q, 90); model.state.items.Add(item); model.Unlock(b.id);
                if (worn[(int)b.slot] == b.id) model.Equip(item.uid);
            }
            model.state.unlockedRegion = Catalog.LastRegion; model.state.bossMask = 31; model.state.bosses = Mathf.Max(model.state.bosses, 5);
            model.state.scrap = Mathf.Max(model.state.scrap, 999999); model.state.cores = Mathf.Max(model.state.cores, 999);
            unseen.Clear(); selectedItem = model.Equipped(Slot.Weapon).uid; combat.ClampHealth(); Save();
            Notify("测试：全套装备 · " + Catalog.QualityNames[allGearQuality] + " · " + model.state.items.Count + " 件（再按 F10 换下一档品质）", 4);
        }
        private void GrantTestOutfit()
        {
            string[] set = TestOutfits[testOutfit++ % TestOutfits.Length];
            foreach (string id in set)
            {
                Item item = model.state.items.FirstOrDefault(x => x.baseId == id);
                if (item == null) { item = model.Create(id, Quality.Common, 1); model.state.items.Add(item); model.Unlock(id); }
                model.Equip(item.uid);
            }
            Notify("测试外观：" + string.Join(" / ", set), 3);
        }

        // v0.20 卡关提示：在最前面那一关连着倒 3 次起，按情况给一句建议（同一关 10 分钟内只说一次）。
        private int hintRegion = -1; private float hintAt = -999;
        private void StuckHint()
        {
            int r = model.state.retryRegion;
            if (r != model.state.unlockedRegion || model.state.retryStreak < 3) return;
            if (r == hintRegion && clock - hintAt < 600) return;
            hintRegion = r; hintAt = clock; Notify(StuckHintText(), 10);
        }
        public string StuckHintText()
        {
            Item w = model.Equipped(Slot.Weapon);
            bool single = w != null && !Catalog.IsGroupWeapon(w.baseId);
            bool boss = Chapters.Stage(model.state.retryRegion).kind == StageKind.Boss;
            if (single && !boss) return "卡关提示：这一关的怪成群地来，单体武器很吃亏。换一把标着「群伤」的武器（整备页武器详情里看，手炮、重锤、撞杆矛、霰弹、线圈这几类）会轻松很多。";
            if (model.AnyPerkBuyable) return "卡关提示：工坊的改造板还有买得起的节点，点上再打会轻松不少。";   // 11-24 节点和义体是后几章最吃的两样（提升来源对照）
            if (model.AnyImplantUpgradable) return "卡关提示：整备页左边的义体可以升级了，碎片已经够了。";
            if (model.EchoUnlocked && model.state.retryStreak >= 5) return "卡关提示：可以先去零号深网刷一会（选关最下面）——每层都有掉落，还能攒织网碎片升义体。";
            if (model.ReforgeUnlocked) return "卡关提示：工坊可以洗炼词条，把用不上的词条换成攻击、暴击或生命。";
            return "卡关提示：开着自动推进会先回刷前一关攒零件，升级装备、升改造板变强后再自动挑战。";
        }
        /// 测试用（桥接 call TestOpenHelp）：打开行动指南。
        public void TestOpenHelp() { navigation.Open(GameModal.Help); }
        public void TestOpenSettings() { navigation.Open(GameModal.Settings); }
        /// v0.21 测试：打开任意弹窗（GameModal 的序号：1 选关 4 指南 6 分解 7 离线 …）。
        public void TestModal(int m) { if (m == (int)GameModal.Offline) { offlineAway = 5400; offlineGain = 1234; } navigation.Open((GameModal)m); }
        /// 测试用（桥接 call TestStuckHint）：直接弹一条卡关提示。
        public void TestStuckHint() { model.state.retryRegion = model.state.unlockedRegion; model.state.retryStreak = 3; hintRegion = -1; StuckHint(); }

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
            if (region < 0 || region > model.state.unlockedRegion) return;
            if (show) { navigation.Close(); Go(GameScreen.Action); }
            BeginStageTransition(region);
        }

        private void OnGUI()
        {
            if (model == null) return;
            Ui.Begin();
            Event current = Event.current;
            ExhibitKey(current);   // v0.21 展览模式：Ctrl+Shift+E 退出
            if (titleOpen)
            {   // v0.21 标题画面：Esc 关掉上面的弹层
                if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape) { if (exhibitPanel || exhibitExitPanel) exhibitPanel = exhibitExitPanel = false; else if (titleConfirmNew) titleConfirmNew = false; else if (creditsOpen) creditsOpen = false; else navigation.Close(); current.Use(); }
                TitleScreen(); ExhibitPanels(); return;
            }
            if (openingAt >= 0) { OpeningScreen(); return; }
            if (current.type == EventType.KeyDown && (exhibitPanel || exhibitExitPanel))
            { if (current.keyCode == KeyCode.Escape) { exhibitPanel = exhibitExitPanel = false; current.Use(); } }
            else if (current.type == EventType.KeyDown && ExhibitMode && current.keyCode >= KeyCode.F1 && current.keyCode <= KeyCode.F12) current.Use();   // v0.21 展览模式：开发键全关
            else if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.Escape && navigation.HasModal) { navigation.Close(); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Space) { paused = !paused; current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Alpha1) { Go(GameScreen.Action); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Alpha2) { Go(GameScreen.Loadout); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Alpha3) { Go(GameScreen.Wardrobe); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.Alpha4) { Go(GameScreen.Workshop); current.Use(); }
                else if (!navigation.HasModal && current.keyCode == KeyCode.F2) { GrantTestOutfit(); current.Use(); }
                else if (navigation.Screen == GameScreen.Action && !navigation.HasModal && current.keyCode == KeyCode.F4) { PreviewTransition(); current.Use(); }
                else if (current.keyCode == KeyCode.F6) { GameModel.TestPower = !GameModel.TestPower; combat.ClampHealth(); if (GameModel.TestPower) combat.health = model.Total().health; Notify(GameModel.TestPower ? "测试：无敌 + 攻击 ×10（再按 F6 关闭）" : "测试模式已关闭", 2.5f); current.Use(); }
                else if (current.keyCode == KeyCode.F7) { TestNextAscension(); current.Use(); }
                else if (current.keyCode == KeyCode.F10) { GrantAllGear(); current.Use(); }
                else if (current.keyCode == KeyCode.F8) { SwitchSaveSlot(); current.Use(); }
                else if (current.keyCode == KeyCode.F9 && testSave) { saves.Delete(); RestartGame(); current.Use(); }
                else if (current.keyCode == KeyCode.F5) { model.state.unlockedRegion = Catalog.LastRegion; Save(); Notify("测试：" + Chapters.ChapterCount + " 章 " + (Catalog.LastRegion + 1) + " 关已全部解锁（点关卡名打开地图切章）", 2.5f); current.Use(); }
                else if (current.keyCode == KeyCode.F3) { UiTheme.Next(); Notify("界面配色：" + UiTheme.Name + "（F3 切换）", 2.5f); current.Use(); }
            }
            Ui.FillScreen(Ui.Ground);
            bool modal = navigation.HasModal, exhibitUi = exhibitPanel || exhibitExitPanel;
            Ui.Blocked = modal || exhibitUi;
            switch (navigation.Screen)
            {
                case GameScreen.Action: ActionPage(); break;
                case GameScreen.Loadout: LoadoutPage(); break;
                case GameScreen.Wardrobe: WardrobePage(); break;
                case GameScreen.Workshop: WorkshopPage(); break;
            }
            // 行动页：左侧导航栏跟着终端界面一起滑出 / 收起（纯净模式时藏在屏幕外）。
            Matrix4x4 railMatrix = GUI.matrix;
            if (navigation.Screen == GameScreen.Action)
                GUI.matrix = Matrix4x4.Translate(new Vector3(-(1 - HudEase) * 110 * Ui.K, 0, 0)) * railMatrix;
            Rail();
            GUI.matrix = railMatrix;
            Toast();
            Tips();   // v0.20 新手提示
            if (testSave) TestSaveBadge();
            ExhibitIdleBanner();
            Ui.Blocked = exhibitUi;
            if (modal && navigation.HasModal) Modal();
            Ui.Blocked = false;
            ExhibitPanels();
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
                if (i == 1 && unseen.Count == 0 && model.AnyImplantUpgradable) { Ui.Fill(66, y + 10, 14, 14, Ui.Ink); Ui.Fill(68, y + 12, 10, 10, Ui.Orange); }   // 11-24 整备：有义体能升级
                if (i == 3 && (model.AnyPerkBuyable || model.AnyRobotAssemblable || model.AnyChipToSocket)) { Ui.Fill(66, y + 10, 14, 14, Ui.Ink); Ui.Fill(68, y + 12, 10, 10, Ui.Orange); }   // 11-24 工坊有东西能做（改造板节点买得起、机器人能组装、有芯片没插）：亮一个橙点
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
            float slide = Mathf.Clamp01((clock - toastFrom) / .18f), y = (navigation.Screen == GameScreen.Action ? 692 : 836) - (1 - slide) * 14;   // 11-23 其它页 812 → 836：原来正好盖住仓库最下面一排装备的名字   // 行动页放到地面下方，不挡头顶血条和首领血条
            Item item = FindItem(toastItem);
            if (item != null)
            {
                EquipmentBase b = Catalog.Find(item.baseId); Color quality = QualityVisual.Colors[(int)item.quality];
                // 11-23 压成一行（高 48）：原来两行高 64，在行动页会压到下面「最近回收」那一栏
                float tw0 = Ui.Width(toast, 13, true), nw = Ui.Width(b.name, 16, true), width = tw0 + nw + 190, x = 800 - width / 2;
                Ui.Fill(x, y, width, 48, Ui.Ink); Ui.Fill(x, y + 45, width, 3, quality);
                Ui.ItemTile(x + 6, y + 5, 38, 38, b, item.quality, false, false, 2, false);
                Ui.Text(x + 56, y, tw0 + 4, 45, toast, 13, quality, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 56 + tw0 + 14, y, nw + 4, 45, b.name, 16, Ui.Panel, TextAnchor.MiddleLeft, true);
                if (Ui.Button(x + width - 92, y + 6, 84, 33, "查看", 3, true, 14)) OpenItem(item);
            }
            else
            {
                float tw = Ui.Width(toast, 15, true); bool two = tw + 56 > 760;   // v0.20 长提示（卡关提示）换成两行
                float width = Mathf.Min(760, tw + 56), h = two ? Mathf.Max(74, Ui.Height(toast, 15, width - 40, true) + 22) : 52, x = 800 - width / 2;   // 11-23 译文三行时框跟着变高
                if (y + h > 896) y = 896 - h;
                Ui.Fill(x, y, width, h, Ui.Ink); Ui.Fill(x, y, 6, h, Ui.Orange);
                Ui.Text(x + 24, y + (two ? 9 : 0), width - 40, two ? h - 14 : 52, toast, 15, Ui.Panel, two ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, true, false, two);
            }
        }

        // 整备和衣柜里的角色预览：按外套外观显示整帧，持近战武器外观时用持刃站姿。缺图时退回旧绘制。
        private void PreviewHero(float feetX, float feetY, float canvas, float oldX, float oldY, float oldW, float oldH)
        {
            if(model.AscensionStage>=2)feetX+=canvas*.025f;   // v0.20 飞升 60% 起背后有散热鳍，人往右挪一点，鳍不出框
            if(LayeredRigView.CanDraw(model)){LayeredRigView.Draw(model,feetX,feetY,canvas/HeroCanvas,LayeredRigView.Anim.Idle,clock/1.2f,Color.white);return;}
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

        /// 前 6 项是基础属性，后 4 项是 v0.18 的特殊词条（暴击伤害、特效伤害、首领伤害、零件回收），顺序同 Catalog.StatNames。
        private static float[] Values(Stats s) { return new[] { s.health, s.attack, s.defense, s.haste, s.crit, s.luck, s.critDamage, s.effectDamage, s.bossDamage, s.scrapBonus }; }
        private static string Value(int stat, float value) { return stat == 3 || stat == 4 || stat >= Balance.BasicStats ? (value * 100).ToString("F1") + "%" : value.ToString("F1"); }
        private static void SplitEffect(Effect effect, out string name, out string description)
        {
            string full = Catalog.EffectName(effect); int cut = full.IndexOf('：');
            if (effect == Effect.None || cut < 0) { name = "无固定效果"; description = "这件装备只提供属性。"; }
            else { name = full.Substring(0, cut); description = full.Substring(cut + 1); }
        }
    }
}
