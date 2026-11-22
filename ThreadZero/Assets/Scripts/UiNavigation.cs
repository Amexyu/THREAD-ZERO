namespace ThreadZero
{
    public enum GameScreen { Action, Loadout, Wardrobe, Workshop }
    public enum GameModal { None, Map, Stats, Settings, Help, ItemRolls, Salvage, Offline, Epilogue, Socket }   // v0.21 Socket：芯片槽 / 改装位
    public sealed class UiNavigation
    {
        public GameScreen Screen { get; private set; }
        public GameModal Modal { get; private set; }
        public bool HasModal { get { return Modal!=GameModal.None; } }
        public void Go(GameScreen screen) { Screen=screen;Modal=GameModal.None; }
        public void Open(GameModal modal) { Modal=modal; }
        public void Close() { Modal=GameModal.None; }
    }
}
