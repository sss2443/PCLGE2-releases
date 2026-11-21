using System.Windows.Media;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Pclge.Core;
using Pclge.Ui;

namespace Pclge.Views;

/// <summary>设置页：启动器 / Java / 下载 / 版本管理 / 关于。</summary>
public sealed class SetupPage : IPage
{
    private readonly MainWindow _shell;

    private readonly TextBlock _gameDirText = UiKit.Label("", UiKit.Muted);
    private readonly TextBox _memory = UiKit.Input("", 90);
    private readonly CheckBox _isolation = new() { Content = "版本隔离（每个版本独立 mods / 存档 / 配置）", Margin = new Thickness(0, 4, 0, 4) };
    private readonly CheckBox _fileLog = new() { Content = "把启动日志写入文件", Margin = new Thickness(0, 4, 0, 4) };

    private readonly ComboBox _font = UiKit.Combo(200);

    private readonly ListBox _javaList = UiKit.List(220);
    private readonly TextBlock _javaInfo = UiKit.Note("");

    private readonly ComboBox _source = UiKit.Combo(190);
    private readonly TextBox _threads = UiKit.Input("12", 70);

    private readonly ListBox _instances = UiKit.List(320);
    private readonly ListBox _mods = UiKit.List();
    private readonly ListBox _packs = UiKit.List();
    private readonly ListBox _saves = UiKit.List();
    private readonly TextBlock _instanceInfo = UiKit.Note("");

    public SetupPage(MainWindow shell) => _shell = shell;

    public string Title => "设置";

    public Grid Build()
    {
        var panes = new List<UIElement>
        {
            BuildLauncherPane(),
            BuildJavaPane(),
            BuildDownloadPane(),
            BuildManagePane(),
            BuildAboutPane()
        };
        var nav = UiKit.List(170);
        var host = UiKit.SubPager(panes, nav, "启动器", "Java", "下载", "版本管理", "关于");

        var left = new StackPanel();
        left.Children.Add(UiKit.Heading("设置"));
        left.Children.Add(nav);
        left.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
        left.Children.Add(UiKit.Button("打开配置文件", (_, _) => MainWindow.OpenFolder(Config.LauncherDir)));

        return UiKit.TwoPane(left, host);
    }

    private UIElement BuildLauncherPane()
    {
        var panel = new StackPanel();
        var dirRow = UiKit.Row(
            UiKit.PrimaryButton("选择 .minecraft 目录", (_, _) => ChooseGameDir()),
            UiKit.Button("恢复默认", (_, _) =>
            {
                Config.ResetGameDir();
                Refresh();
                _shell.Status("已恢复默认游戏目录");
            }),
            UiKit.Button("打开目录", (_, _) => MainWindow.OpenFolder(Config.GameDir))
        );
        panel.Children.Add(UiKit.Card("游戏目录", _gameDirText, dirRow));

        var memoryRow = UiKit.Row(UiKit.Label("默认内存 (MB)"), _memory, UiKit.Button("保存", (_, _) =>
        {
            if (!int.TryParse(_memory.Text.Trim(), out var mb) || mb < 256) mb = 2048;
            Config.Put("memory", mb.ToString());
            _shell.Status("默认内存已设为 " + mb + "MB");
        }));
        panel.Children.Add(UiKit.Card("启动", memoryRow, _isolation, _fileLog));

        _font.Items.Add("Minecraft 像素字体（内嵌）");
        _font.Items.Add("微软雅黑（默认中文界面）");
        _font.SelectedIndex = string.Equals(Config.Get("uiFont", "minecraft"), "yahei", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        panel.Children.Add(UiKit.Card("界面字体",
            UiKit.Row(UiKit.Label("字体"), _font, UiKit.Button("应用", (_, _) => ApplyFont())),
            UiKit.Note("像素字体来自 Minecraft_Next_Font_12px2.0.ttf，已内嵌进程序；它没有中文字形，中文自动回退到微软雅黑。"),
            UiKit.Note("切换后重启 PCLGE 才会整套界面生效。")));

        panel.Children.Add(UiKit.Card("存档迁移",
            UiKit.Note("把旧游戏目录 saves 里的世界复制到当前目录（同名跳过，不动原文件）。"),
            UiKit.Button("从旧目录导入存档", (_, _) => ImportSaves())
        ));

        var maintainRow = UiKit.Row(
            UiKit.Button("清理未完成的下载（.tmp）", (_, _) => CleanTemp()),
            UiKit.Button("备份存档为 zip", (_, _) => BackupAllSaves())
        );
        panel.Children.Add(UiKit.Card("维护", maintainRow));
        return UiKit.Scroll(panel);
    }

    private void ApplyFont()
    {
        var yahei = _font.SelectedIndex == 1;
        Config.Put("uiFont", yahei ? "yahei" : "minecraft");
        UiKit.Font = yahei ? UiKit.YaHei : UiKit.McFont;
        _shell.FontFamily = UiKit.Font;
        _shell.Status("界面字体已切换为" + (yahei ? "微软雅黑" : "Minecraft 像素字体") + "，重启启动器后全部界面生效");
    }

    private UIElement BuildJavaPane()
    {
        var panel = new StackPanel();
        var javaRow = UiKit.Row(
            UiKit.PrimaryButton("重新扫描", (_, _) =>
            {
                JavaService.Scan(true);
                Refresh();
                _shell.Status("Java 扫描完成");
            }),
            UiKit.Button("手动指定 javaw.exe", (_, _) => PickJava()),
            UiKit.Button("设为当前使用", (_, _) => UseSelectedJava())
        );
        panel.Children.Add(UiKit.Card("检测到的 Java",
            _javaList,
            javaRow,
            _javaInfo,
            UiKit.Note("1.17 以下一般用 Java 8；1.18-1.20.4 用 Java 17；1.20.5 以上用 Java 21。")
        ));
        return UiKit.Scroll(panel);
    }

    private UIElement BuildDownloadPane()
    {
        _source.Items.Add("BMCLAPI 镜像（国内快）");
        _source.Items.Add("官方源（Mojang）");
        var panel = new StackPanel();
        var saveButton = UiKit.PrimaryButton("保存下载设置", (_, _) =>
        {
            Config.Put("source", _source.SelectedIndex == 0 ? "1" : "0");
            if (int.TryParse(_threads.Text.Trim(), out var t)) Config.Put("threads", Math.Clamp(t, 1, 32).ToString());
            _shell.Status("下载设置已保存");
        });
        panel.Children.Add(UiKit.Card("下载",
            UiKit.Row(UiKit.Label("默认下载源"), _source),
            UiKit.Row(UiKit.Label("并发下载线程"), _threads),
            saveButton,
            UiKit.Note("镜像只影响游戏本体、依赖库和资源文件；Modrinth 走官方 CDN。")
        ));
        return UiKit.Scroll(panel);
    }

    private UIElement BuildManagePane()
    {
        _instances.SelectionChanged += (_, _) => LoadInstance();
        var panel = new StackPanel();
        var instanceRow = UiKit.Row(UiKit.Label("版本"), _instances, UiKit.Button("刷新", (_, _) => Refresh()));
        var openRow = UiKit.Row(
            UiKit.Button("打开实例目录", (_, _) => OpenInstanceDir()),
            UiKit.Button("打开日志", (_, _) => OpenInstanceLog())
        );
        panel.Children.Add(UiKit.Card("实例", instanceRow, _instanceInfo, openRow));

        var modPanel = new StackPanel();
        modPanel.Children.Add(UiKit.Row(
            UiKit.Button("启用 / 禁用", (_, _) => ToggleMod()),
            UiKit.Button("添加 .jar", (_, _) => AddMod()),
            UiKit.Button("删除", (_, _) => DeleteMod())
        ));
        modPanel.Children.Add(_mods);

        var packPanel = new StackPanel();
        packPanel.Children.Add(UiKit.Row(
            UiKit.Button("添加资源包 .zip", (_, _) => AddPack()),
            UiKit.Button("删除", (_, _) => DeletePack())
        ));
        packPanel.Children.Add(_packs);

        var savePanel = new StackPanel();
        savePanel.Children.Add(UiKit.Row(
            UiKit.Button("备份为 zip", (_, _) => BackupSave()),
            UiKit.Button("删除存档", (_, _) => DeleteSave()),
            UiKit.Button("打开存档目录", (_, _) => OpenSavesDir())
        ));
        savePanel.Children.Add(_saves);

        var tabs = new TabControl { Background = Brushes.White, Height = 330 };
        tabs.Items.Add(new TabItem { Header = "模组", Content = modPanel });
        tabs.Items.Add(new TabItem { Header = "资源包", Content = packPanel });
        tabs.Items.Add(new TabItem { Header = "存档", Content = savePanel });
        panel.Children.Add(UiKit.Card("版本内容", tabs));
        return UiKit.Scroll(panel);
    }

    private UIElement BuildAboutPane()
    {
        var panel = new StackPanel();
        var links = UiKit.Row(
            UiKit.PrimaryButton("打开 GitHub 仓库", (_, _) => MainWindow.OpenUrl("https://github.com/sss2443/PCLGE2-releases")),
            UiKit.Button("打开配置目录", (_, _) => MainWindow.OpenFolder(Config.LauncherDir))
        );
        panel.Children.Add(UiKit.Card("关于 PCLGE",
            UiKit.Label("PCLGE 1.00 · C# / WPF 完整版"),
            UiKit.Label("原版游戏下载 · 四种模组加载器 · Modrinth 商城 · 皮肤 · 局域网联机 · 崩溃分析"),
            UiKit.Note("配置文件：" + Config.ConfigFile),
            UiKit.Note("日志文件：" + Path.Combine(Config.LogDir, "latest.log")),
            links
        ));
        panel.Children.Add(UiKit.Card("配置文件",
            UiKit.Note("配置为纯文本 ini，可直接编辑。删除 pclge-config.ini 会重置所有设置（存档不受影响）。")
        ));
        return UiKit.Scroll(panel);
    }

    // ================= 刷新 =================
    public void Refresh()
    {
        _gameDirText.Text = Config.GameDir;
        _memory.Text = Config.MemoryMb.ToString();
        _isolation.IsChecked = Config.Isolation;
        _fileLog.IsChecked = Config.GetBool("fileLog", true);
        _isolation.Checked += (_, _) => Config.Put("isolation", "1");
        _isolation.Unchecked += (_, _) => Config.Put("isolation", "0");
        _fileLog.Checked += (_, _) => Config.Put("fileLog", "1");
        _fileLog.Unchecked += (_, _) => Config.Put("fileLog", "0");

        _source.SelectedIndex = Config.UseBmcl ? 0 : 1;
        _threads.Text = Config.DownloadThreads.ToString();

        var javas = JavaService.Scan();
        var selected = _javaList.SelectedIndex;
        _javaList.ItemsSource = javas.Select(j => j.Display).ToList();
        if (selected >= 0 && selected < javas.Count) _javaList.SelectedIndex = selected;
        else if (javas.Count > 0) _javaList.SelectedIndex = 0;
        _javaInfo.Text = javas.Count == 0
            ? "没有找到 Java，请手动指定 javaw.exe 路径。"
            : $"共 {javas.Count} 个 Java，当前使用：{JavaService.Resolve()}";

        var installed = VersionService.Installed();
        var currentSelection = _instances.SelectedItem as string;
        _instances.ItemsSource = installed;
        if (currentSelection != null && installed.Contains(currentSelection)) _instances.SelectedItem = currentSelection;
        else if (installed.Count > 0) _instances.SelectedIndex = 0;
        LoadInstance();
    }

    private string CurrentInstance => _instances.SelectedItem as string;

    private void LoadInstance()
    {
        var id = CurrentInstance;
        if (id == null)
        {
            _instanceInfo.Text = "还没有安装任何版本";
            return;
        }
        var detail = VersionService.Inspect(id);
        _instanceInfo.Text = $\"{id} · {detail.Loader} · {detail.Type} · {detail.Size / 1048576.0:F1} MB\";
        _mods.ItemsSource = ModManager.ListMods(id)
            .Select(m => (m.Enabled ? "[开] " : "[关] ") + m.FileName).ToList();
        _packs.ItemsSource = ModManager.ListResourcePacks(id);
        _saves.ItemsSource = ModManager.ListSaves(id);
    }

    // ================= 操作 =================
    private void ChooseGameDir()
    {
        var dialog = new OpenFolderDialog { Title = "选择 .minecraft 目录" };
        if (dialog.ShowDialog() != true) return;
        Config.SetGameDir(dialog.FolderName);
        Refresh();
        _shell.Status("游戏目录已改为：" + dialog.FolderName);
    }

    private void ImportSaves()
    {
        var dialog = new OpenFolderDialog { Title = "选择旧游戏目录（含 saves）" };
        if (dialog.ShowDialog() != true) return;
        var id = CurrentInstance;
        if (id == null)
        {
            _shell.Status("先在「版本管理」里选一个版本");
            return;
        }
        var source = Path.Combine(dialog.FolderName, "saves");
        if (!Directory.Exists(source))
        {
            _shell.Status("该目录下没有 saves 文件夹");
            return;
        }
        _shell.Status("复制存档中...");
        _shell.RunAsync(() =>
        {
            var count = ModManager.ImportSaves(id, source);
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                _shell.Status($\"已复制 {count} 个世界到 {id}\");
                LoadInstance();
            }));
        });
    }

    private void CleanTemp()
    {
        _shell.RunAsync(() =>
        {
            var count = 0;
            foreach (var dir in new[] { Config.LibrariesDir, Config.AssetsDir, Config.InstallerDir, Config.VersionsDir })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); count++; } catch { }
                }
            }
            _shell.Dispatcher.BeginInvoke(new Action(() => _shell.Status($\"已清理 {count} 个临时文件\")));
        });
    }

    private void BackupAllSaves()
    {
        var id = CurrentInstance;
        if (id == null) return;
        _shell.RunAsync(() =>
        {
            var saves = ModManager.ListSaves(id);
            var count = 0;
            foreach (var save in saves)
            {
                try { ModManager.BackupSaves(id, save); count++; } catch { }
            }
            _shell.Dispatcher.BeginInvoke(new Action(() =>
                _shell.Status($\"已备份 {count} 个存档到 {Path.Combine(Config.LauncherDir, \"backups\")}\")));
        });
    }

    private void PickJava()
    {
        var dialog = new OpenFileDialog { Title = "选择 javaw.exe", Filter = "Java|java*.exe" };
        if (dialog.ShowDialog() != true) return;
        Config.Put("javaPath", dialog.FileName);
        Refresh();
        _shell.Status("已指定 Java：" + dialog.FileName);
    }

    private void UseSelectedJava()
    {
        var list = JavaService.Scan();
        var index = _javaList.SelectedIndex;
        if (index < 0 || index >= list.Count) return;
        Config.Put("javaPath", list[index].Exe);
        Refresh();
        _shell.Status("当前 Java：" + list[index].Exe);
    }

    private void ToggleMod()
    {
        var id = CurrentInstance;
        if (id == null || _mods.SelectedItem is not string item) return;
        try
        {
            ModManager.Toggle(id, item[(item.IndexOf(']') + 2)..]);
            LoadInstance();
        }
        catch (Exception ex)
        {
            _shell.Status("切换失败：" + ex.Message);
        }
    }

    private void AddMod()
    {
        var id = CurrentInstance;
        if (id == null) return;
        var dialog = new OpenFileDialog { Title = "选择模组", Filter = "模组|*.jar" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            ModManager.Add(id, dialog.FileName);
            LoadInstance();
            _shell.Status("已添加：" + Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            _shell.Status("添加失败：" + ex.Message);
        }
    }

    private void DeleteMod()
    {
        var id = CurrentInstance;
        if (id == null || _mods.SelectedItem is not string item) return;
        var file = item[(item.IndexOf(']') + 2)..];
        try
        {
            ModManager.Delete(id, file);
            LoadInstance();
            _shell.Status("已删除：" + file);
        }
        catch (Exception ex)
        {
            _shell.Status("删除失败：" + ex.Message);
        }
    }

    private void AddPack()
    {
        var id = CurrentInstance;
        if (id == null) return;
        var dialog = new OpenFileDialog { Title = "选择资源包", Filter = "资源包|*.zip" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            ModManager.AddResourcePack(id, dialog.FileName);
            LoadInstance();
            _shell.Status("已添加资源包：" + Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            _shell.Status("添加失败：" + ex.Message);
        }
    }

    private void DeletePack()
    {
        var id = CurrentInstance;
        if (id == null || _packs.SelectedItem is not string file) return;
        try
        {
            ModManager.DeleteResourcePack(id, file);
            LoadInstance();
        }
        catch (Exception ex)
        {
            _shell.Status("删除失败：" + ex.Message);
        }
    }

    private void BackupSave()
    {
        var id = CurrentInstance;
        if (id == null || _saves.SelectedItem is not string save) return;
        _shell.RunAsync(() =>
        {
            var path = ModManager.BackupSaves(id, save);
            _shell.Dispatcher.BeginInvoke(new Action(() => _shell.Status("存档已备份：" + path)));
        });
    }

    private void DeleteSave()
    {
        var id = CurrentInstance;
        if (id == null || _saves.SelectedItem is not string save) return;
        if (MessageBox.Show($\"删除存档 {save}？此操作不可恢复。\", \"PCLGE\",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try
        {
            ModManager.DeleteSave(id, save);
            LoadInstance();
        }
        catch (Exception ex)
        {
            _shell.Status("删除失败：" + ex.Message);
        }
    }

    private void OpenInstanceDir()
    {
        var id = CurrentInstance;
        if (id != null) MainWindow.OpenFolder(ModManager.GameDir(id));
    }

    private void OpenInstanceLog()
    {
        var id = CurrentInstance;
        if (id == null) return;
        var log = Path.Combine(ModManager.GameDir(id), "logs", "latest.log");
        if (!File.Exists(log))
        {
            _shell.Status("还没有游戏日志，先启动一次游戏");
            return;
        }
        MainWindow.OpenFolder(Path.GetDirectoryName(log)!);
    }

    private void OpenSavesDir()
    {
        var id = CurrentInstance;
        if (id != null) MainWindow.OpenFolder(ModManager.SavesDir(id));
    }
}
