# 仓库与工程入口

2026-11-16 已将当前项目复制到用户创建的 THREAD-ZERO 仓库，原目录保留。394个原始文件已做SHA256副本核对；整理时约46MB。

- GitHub Desktop仓库：`F:\毕业制作\THREAD-ZERO`。
- Unity Hub添加/打开：`F:\毕业制作\THREAD-ZERO\ThreadZero`，即含Assets、Packages、ProjectSettings的目录。
- Claude/Codex后续工作区统一设为仓库根 `F:\毕业制作\THREAD-ZERO`，避免继续在外层旧副本修改后忘记同步。
- 原来的 `F:\毕业制作\ThreadZero` 未删除、未移动，是整理前的保留副本，不与新工程自动同步。

提交范围包括Unity源码/资源及meta、包配置、项目设置、文档、工具、测试和本轮初始匕首素材。未复制Unity Library/Temp/Logs/UserSettings、Backups、Artifacts、私有工具缓存和构建目录；忽略规则已检查。保存394文件后本说明额外作为新文档加入。

没有执行git commit或push。可在GitHub Desktop检查Changes，再由用户决定提交与推送。

当前匕首查看入口是仓库根 `ReviewStarterDagger.cmd` 或Unity菜单 `Thread Zero > Review Starter Dagger (Unsaved)`。启动隔离测试局，不写正式存档。后台Unity许可IPC阻塞的限制见 `DaggerRuntimeIntegration_v12.md`。
