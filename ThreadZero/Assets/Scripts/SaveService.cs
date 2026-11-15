using System;
using System.IO;

namespace ThreadZero
{
    public sealed class SaveService
    {
        public readonly string path;
        private readonly Func<string, GameState> deserialize;
        private readonly Func<GameState, string> serialize;
        private readonly Action<string> report;
        public bool Writable { get; private set; } = true;
        public string Message { get; private set; } = "本地自动保存";
        public SaveService(string directory, Func<string, GameState> reader, Func<GameState, string> writer, Action<string> logger = null)
        {
            path = Path.Combine(directory, "thread-zero-save.json");
            deserialize = reader ?? throw new ArgumentNullException(nameof(reader));
            serialize = writer ?? throw new ArgumentNullException(nameof(writer));
            report = logger;
        }
        public GameState Load()
        {
            bool any = false;
            foreach (string file in new[] { path, path + ".bak" })
            {
                if (!File.Exists(file)) continue;
                any = true;
                try
                {
                    string json = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"version\"")) throw new Exception("存档缺少版本号");
                    GameState data = deserialize(json);
                    if (data == null) throw new Exception("空存档");
                    if (data.version > 1) { Writable = false; Message = "存档版本较新，已保护原文件；本次为临时进度"; return null; }
                    new GameModel(data, 1);
                    if (file.EndsWith(".bak"))
                    {
                        // Preserve the broken primary before the next save replaces it.
                        if (File.Exists(path)) File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks, false);
                        File.Copy(file, path, true);
                        Message = "主存档损坏，已从备份恢复";
                    }
                    return data;
                }
                catch (Exception ex) { report?.Invoke("读档失败 " + file + ": " + ex.Message); }
            }
            if (any) { Writable = false; Message = "存档损坏，原文件已保留；本次为临时进度"; }
            return null;
        }
        public bool Save(GameState data)
        {
            if (!Writable) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, serialize(data));
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
                return true;
            }
            catch (Exception ex) { Message = "保存失败：" + ex.Message; report?.Invoke(Message); return false; }
        }
    }
}
