# PCLGE 2 · PCLGE 1.00（C# / WPF 完整版）

PCLGE 是一个用 **C# / .NET 10 + WPF** 写的《我的世界》启动器，界面结构对齐 PCL：顶部六个一级导航（**启动 / 基岩版 / 下载 / 设置 / 工具 / 联机**），每页左列表 + 右内容，底部常驻运行日志与状态栏。界面配色与控件规格取自 **PCL CE**。

> 本仓库是发布版（releases）：源码 + 编译好的单文件 exe。

## 功能

### 启动
- 自动扫描已安装版本，显示加载器 / 类型 / 占用空间
- 多离线账户管理（新建 / 删除 / 切换），离线 UUID 与官方算法一致
- 内存、额外 JVM 参数、版本隔离（每个版本独立 mods / saves / 配置）
- 按 MC 版本自动挑选 Java（1.17+ → 17，1.20.5+ → 21）
- 完整解析版本 JSON：`arguments.game` / `arguments.jvm`、rules、继承链、老版本 `minecraftArguments`
- 原生库解压、`-cp` 自动拼装、启动后等待退出并显示退出码

### 下载
- 原版游戏：BMCLAPI 镜像 / Mojang 官方源切换，按正式版 / 快照 / 关键词过滤
- 模组加载器：**Fabric / Quilt / Forge / NeoForge / OptiFine**
- Modrinth：模组、资源包、光影、整合包搜索（按 MC 版本 + 加载器筛选）
- 整合包按 `modrinth.index.json` 逐文件下载，并解压 `overrides/`
- 已安装管理：查看详情、修复重装、删除版本、打开目录

### 设置
- 游戏目录自定义 / 恢复默认、存档迁移、存档全量备份为 zip
- 界面字体切换（Minecraft 像素字体 / 微软雅黑）
- Java 扫描（JAVA_HOME / PATH / Program Files 各发行版）+ 手动指定 + 设为默认
- 下载源、并发线程、清理 `.tmp` 残留
- 版本管理：模组启用 / 禁用 / 添加 / 删除，资源包、存档备份与删除

### 工具
- **皮肤**：LittleSkin 下载、本地 PNG 导入、应用到实例、预览
- **成就图**：生成 320×64 原版成就弹窗风格 PNG
- **崩溃分析**：读取 `crash-reports/*.txt` 与 `logs/latest.log`，匹配 15 类常见原因，支持导出报告
- **日志**：启动器日志实时展示、落盘、清空、打开游戏日志

### 联机
- UDP 25567 局域网广播聊天：`[CHAT]昵称||消息`、`[PING]`、`[BYE]`
- 在线玩家列表、心跳、端口可改、同机多开

### 基岩版（客户端 / 服务端）
- **客户端**：`Get-AppxPackage` 检测已装的 `MinecraftUWP` / `WindowsBeta` / 教育版并显示版本与位置
- **客户端安装 / 更新**：`winget install --id 9NBLGGH2JHXJ --source msstore`（微软商店正版渠道），日志实时输出；无 winget 时降级为打开商店页面
- **客户端启动**：`shell:AppsFolder\<AUMID>` 直接拉起
- **服务端**：从官网解析 Windows 版 BDS 下载地址并下载解压、关联已有目录、启动 / 停止、实时输出日志
- `server.properties` 读取 / 编辑 / 端口写入

## 界面

界面不是自绘的，配色、字号、圆角、间距全部取自 **PCL CE**（PCL Community Edition，Apache-2.0）的实现：

| 项目 | 来源 | 取值 |
|---|---|---|
| 主题色 / 灰度 | `PCL.Core/UI/Theme/ToneProfile.cs` + `ThemeService.GetCurrentThemeArgs()`（CatBlue 明色） | 由 OKLCH 换算为 sRGB：ColorBrush1 `#323B47`、ColorBrush2 `#1662B6`、ColorBrush3 `#2078DA`、ColorBrush4 `#5291DF`、ColorBrush7 `#E1ECFA`、Gray3 `#797979`、Gray6 `#E4E4E4`、Gray7 `#EBEBEB`、Gray8 `#F2F2F2`、Background `#FDFDFD` |
| 正文 / 提示字号 | `PCL.Launcher/UI/Styles/PageStyles.axaml` | 正文 13、提示 12、页标题 20 SemiBold |
| 卡片 | 同上（`Border.Card`） | 圆角 5、内边距 13/11、阴影 `0 0 3 0 #12000000` |
| 按钮 | 同上（`Button` / `MyButton`） | 圆角 4、内边距 14,6；主按钮 ColorBrush3 底白字、悬停 ColorBrush4；次按钮半透明白底 + 1px ColorBrush1 描边 |
| 列表项 | 同上（`Border.PageListItem`） | 圆角 6、内边距 8,6、悬停 ColorBrush8、选中 ColorBrush7 |
| 字体 | 同上（`LaunchFontFamily`）+ 内置 Minecraft 字体 | 默认 `Minecraft Next Font`（`Minecraft_Next_Font_12px2.0.ttf`，内嵌），中文字形自动回退 Microsoft YaHei UI |

PCL CE 本身是 Avalonia 项目、样式文件是 `.axaml`；本项目是 WPF，所以没有直接复用它的控件，而是按上表的实测值在 WPF 里复刻同样的观感（圆角、描边、半透明白、悬停/选中色一一对应）。相关实现集中在 `src/PCLGE/Ui/UiKit.cs` 与 `MainWindow.xaml(.cs)`。

## 界面字体

启动器内嵌 `Minecraft_Next_Font_12px2.0.ttf`（Family 名 `Minecraft Next Font`），默认作为界面字体；该字体无中文字形，中文自动回退微软雅黑。

- 设置页 → 启动器 → 「界面字体」可在「Minecraft 像素字体 / 微软雅黑」之间切换，写入 `uiFont` 配置项，重启后整套界面生效。
- 仓库不含二进制字体文件：自行构建时把 ttf 放到 `src/PCLGE/Assets/Fonts/MinecraftNext.ttf`，csproj 会自动内嵌；没有该文件时编译跳过并回退雅黑。

## 目录结构

```
src/PCLGE/
├─ App.xaml / App.xaml.cs          应用入口与全局异常捕获
├─ MainWindow.xaml(.cs)            顶部导航 + 页面容器 + 日志/状态栏
├─ Assets/Fonts/                   内嵌字体（MinecraftNext.ttf，需自备）
├─ Ui/UiKit.cs                     PCL CE 配色 / 卡片 / 按钮 / 列表样式 + 字体族
├─ Views/
│  ├─ LaunchPage.cs                启动
│  ├─ BedrockPage.cs               基岩版
│  ├─ DownloadPage.cs              下载
│  ├─ SetupPage.cs                 设置
│  ├─ ToolsPage.cs                 工具
│  └─ LinkPage.cs                  联机
└─ Core/
   ├─ Config.cs                     ini 配置与目录
   ├─ Net.cs                        HTTP / 下载
   ├─ LogService.cs                 日志缓冲与落盘
   ├─ JavaService.cs                Java 扫描与版本探测
   ├─ AccountService.cs             离线账户与离线 UUID
   ├─ Libraries.cs                  libraries / rules 解析（安装与启动共用）
   ├─ VersionService.cs             版本清单、安装、修复、删除
   ├─ LoaderInstaller.cs            Fabric / Quilt / Forge / NeoForge / OptiFine
   ├─ GameLauncher.cs               拼装参数并启动 Java
   ├─ ModManager.cs                 模组 / 资源包 / 存档
   ├─ Modrinth.cs                   Modrinth 搜索与下载
   ├─ SkinService.cs                LittleSkin 与本地皮肤
   ├─ CrashAnalyzer.cs              崩溃分析规则
   ├─ AchievementImage.cs           成就图渲染
   ├─ BedrockService.cs             BDS 下载与启停、客户端检测与安装
   └─ LanChat.cs                    局域网 UDP 聊天
```

## 编译

需要 **.NET 10 SDK**（Windows）或 Linux 上交叉编译（`EnableWindowsTargeting` 已开启）：

```powershell
cd src/PCLGE
dotnet publish -c Release -r win-x64
```

产物：`src/PCLGE/bin/Release/net10.0-windows/win-x64/publish/PCLGE.exe`（单文件，约 66 MB，自带 .NET 运行时，无需安装）。

也可以直接用仓库根目录的 `build.ps1`：

```powershell
./build.ps1
```

## 配置与数据位置

| 内容 | 路径 |
|---|---|
| 配置文件 | `%AppData%\PCLGE\pclge-config.ini` |
| 启动器日志 | `%AppData%\PCLGE\logs\latest.log` |
| 皮肤 | `%AppData%\PCLGE\skins\<账户>.png` |
| 存档备份 | `%AppData%\PCLGE\backups\` |
| 游戏目录 | 默认 `%AppData%\.minecraft`，可在设置里改 |

## 已知限制

- 只有 Windows 版（WPF）；尚未做深色主题 / 背景图 / 页面动画。
- 界面按 PCL CE 的配色与度量在 WPF 上复刻，并非 Avalonia 原版控件。
- 内置 Minecraft 字体无中文字形，中文由系统雅黑补；仓库不携带二进制字体，自建时需自行放置 ttf。
- 基岩版客户端只走微软商店正版渠道（winget / 商店页面），不提供绕过授权的 appx 直链下载。
- 只支持离线账户登录，没有微软正版登录。
- 皮肤在离线环境下不会自动生效，需要游戏内离线皮肤模组。
- Forge / NeoForge / OptiFine 安装依赖官方 installer，需要本机已有可用 Java。
- 未做自动更新检查。

## 致谢

界面配色、字号、圆角与间距参考 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)（Apache-2.0）。字体 [Minecraft Next Font](https://github.com/) 由用户提供。

## 声明

本项目是个人学习用的启动器，与 Mojang / Microsoft 无关。《Minecraft》相关版权归 Mojang Studios 所有。

## 许可

MIT License，见 [LICENSE](LICENSE)。
