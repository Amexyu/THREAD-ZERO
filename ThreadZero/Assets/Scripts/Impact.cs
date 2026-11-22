using UnityEngine;

namespace ThreadZero
{
    /// v0.20 打击感：顿帧 + 震屏。
    /// 顿帧：重击命中的那一下，战斗停一小会儿（按战斗时间算，跟倍速 / 慢放一起缩放），特效照常播；
    /// 震屏：舞台（背景、角色、怪、特效）整体抖一下，按 GameApp.clock 衰减。UI 不抖。
    public static class Impact
    {
        private static float stopLeft;
        private static float shakeAmp, shakeAt = -10, shakeDur = .3f;
        private static Vector2 shakeDir = Vector2.right;
        /// 设置里的「震屏」：1 完整、.5 减弱、0 关闭（只影响震屏，顿帧不变）。
        public static float Scale = 1;

        public static void Clear() { stopLeft = 0; shakeAmp = 0; shakeAt = -10; }

        /// 顿帧 seconds 秒（取正在进行的和新的较长者）。
        public static void Stop(float seconds) { stopLeft = Mathf.Max(stopLeft, seconds); }

        /// 每一小步战斗推进前调用：先把顿帧吃掉，返回剩下还能推进的时间。
        public static float Absorb(float budget)
        {
            if (stopLeft <= 0) return budget;
            float eat = Mathf.Min(stopLeft, budget); stopLeft -= eat; return budget - eat;
        }

        /// 震屏：amp 逻辑像素，dur 秒内按平方衰减；vertical = 砸地类以上下为主。弱的不会盖掉还在抖的强的。
        public static void Shake(float now, float amp, float dur = .3f, bool vertical = false)
        {
            if (amp < Left(now)) return;
            shakeAmp = amp; shakeAt = now; shakeDur = Mathf.Max(.05f, dur);
            shakeDir = vertical ? new Vector2(.35f, 1) : new Vector2(1, .3f);
        }

        private static float Left(float now)
        {
            float k = (now - shakeAt) / shakeDur;
            return k < 0 || k >= 1 ? 0 : shakeAmp * (1 - k) * (1 - k);
        }

        public static Vector2 Offset(float now)
        {
            float a = Left(now) * Scale; if (a < .3f) return Vector2.zero;
            float t = now - shakeAt;
            return new Vector2(Mathf.Sin(t * 97f) * shakeDir.x, Mathf.Sin(t * 73f + 1.3f) * shakeDir.y) * a;
        }
    }
}
