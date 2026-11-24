# 存档穿梭 SaveShuttle

Slay the Spire 2 的存档导出/导入 mod，界面挂在 **RitsuLib** 的模组配置页里。

## 功能

- **导出全部存档**：打包 `user://default/` 整棵存档树（原版 profile + modded profile + 设置）
- **仅导出进度**：只打包 `profile*/saves/` 与 `profile.save`
- **导入**：列出备份目录里最近的 6 个归档，点一下即可恢复；**导入前会自动把当前存档备份成 `StS2_preimport_<时间>.zip`**

归档是标准 zip：

```
manifest.json                 # 格式版本、创建时间、游戏版本、已加载 mod 列表、每个文件的 sha256
saves/default/<user>/...      # 原样保留 user:// 下的相对路径
```

## 备份目录（运行时自动判定，不写死包名）

| 平台 | 目录 |
|---|---|
| Android | `/sdcard/Android/data/<包名>/files/save_exports/` |
| Windows / macOS / Linux | `<下载目录>/StS2Saves/` |
| 兜底 | `user://save_exports/`（私有目录） |

包名不是硬编码的：从 `user://` 的真实路径（`/data/data/<pkg>/files`）推导，
再用 `/proc/self/cmdline` 与 `ModManager.ModsDirectory` 做双重兜底。

## 安全措施

- 导入前强制自动备份
- zip 条目路径校验，拒绝 `..` / 绝对路径 / 盘符，防止写出存档目录
- manifest 记录每个文件的 sha256，可通过 debug 钩子 `verify` 校验一致性
- 导入后需要重启游戏才生效（存档在启动时读入）

## 构建

```bash
dotnet build -c Release
# 产物：bin/Release/SaveShuttle.dll（与 SaveShuttle.json 一起放进 mods/SaveShuttle/）
```

引用程序集放在 `refs/`（不入库）：`sts2.dll`、`GodotSharp.dll`、
`STS2-RitsuLib.Settings.Ui.Shared.dll`（从游戏发布目录与 RitsuLib 的 `mods/shared/` 复制）。

## 调试钩子

在备份目录的父目录放一个 `savetest.txt`，启动时执行第一条命令：

```
export            # 导出全部
export-progress   # 仅导出进度
list              # 列出归档
import [文件名]    # 导入（默认最新的 full 归档）
corrupt           # 故意破坏 progress.save，用于测试导入
verify            # 用最新 full 归档的 sha256 校验磁盘上的存档
```
