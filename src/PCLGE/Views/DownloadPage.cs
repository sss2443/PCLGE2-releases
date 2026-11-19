using System.IO;
using System.Windows;
using System.Windows.Controls;
using Pclge.Core;
using Pclge.Ui;

namespace Pclge.Views;

/// <summary>下载页：游戏版本 / 整合包 / 模组 / 资源包 / 光影 / 已安装。</summary>
public sealed class DownloadPage : IPage
{
    private readonly MainWindow _shell;

    private readonly ListBox _categories = UiKit.List();
    private readonly ComboBox _source = UiKit.Combo(170);
    private readonly ComboBox _versionFilter = UiKit.Combo(120);
    private readonly TextBox _versionSearch = UiKit.Input("", 160);
    private readonly ListBox _versions = UiKit.List(250);
    private readonly ComboBox _loader = UiKit.Combo(130);
    private readonly ComboBox _loaderVersion = new() { IsEditable = true, Width = 180, Margin = new Thickness(0, 3, 8, 3) };
    private readonly TextBlock _installStatus = UiKit.Note("选择版本后点「下载 / 安装」");

    private readonly TextBox _query = UiKit.Input("", 180);
    private readonly TextBox _targetVersion = UiKit.Input("", 120);
    private readonly ComboBox _modLoader = UiKit.Combo(120);
    private readonly ListBox _results = UiKit.List(330);
    private readonly TextBlock _searchStatus = UiKit.Note("输入关键词搜索 Modrinth");

    private readonly ListBox _installed = UiKit.List(380);
    private readonly TextBlock _installedInfo = UiKit.Note("");

    private readonly Grid _host = new();
    private readonly StackPanel[] _panes = new StackPanel[3];
    private readonly TextBlock _shopTitle = UiKit.Heading("Modrinth");
    private string _shopType = "mod";

    public DownloadPage(MainWindow shell) => _shell = shell;

    public string Title => "下载";

    public Grid Build()
    {
        _source.Items.Add("BMCLAPI 镜像（国内快）");
        _source.Items.Add("官方源（Mojang）");
        _source.SelectedIndex = Config.UseBmcl ? 0 : 1;
        _source.SelectionChanged += (_, _) =>
        {
            Config.Put("source", _source.SelectedIndex == 0 ? "1" : "0");
            _shell.Status("下载源已切换，重新获取版本列表后生效");
        };

        _versionFilter.Items.Add("全部版本");
        _versionFilter.Items.Add("只显示正式版");
        _versionFilter.Items.Add("只显示快照");
        _versionFilter.SelectedIndex = 1;
        _versionFilter.SelectionChanged += (_, _) => RefreshVersions();
        _versionSearch.TextChanged += (_, _) => RefreshVersions();

        _loader.Items.Add("原版（无加载器）");
        _loader.Items.Add("Fabric");
        _loader.Items.Add("Quilt");
        _loader.Items.Add("Forge");
        _loader.Items.Add("NeoForge");
        _loader.Items.Add("OptiFine");
        _loader.SelectedIndex = 0;
        _loader.SelectionChanged += (_, _) => FillLoaderVersions();

        _modLoader.Items.Add("fabric");
        _modLoader.Items.Add("quilt");
        _modLoader.Items.Add("forge");
        _modLoader.Items.Add("neoforge");
        _modLoader.SelectedIndex = 0;

        _results.MouseDoubleClick += (_, _) => InstallFromShop();

        var gamePanel = new StackPanel();
        gamePanel.Children.Add(UiKit.Card("游戏版本",
            UiKit.Row(UiKit.Label("下载源"), _source,
                UiKit.Button("获取 / 刷新版本列表", (_, _) => FetchVersions())),
            UiKit.Row(_versionFilter, _versionSearch),
            _versions,
            UiKit.Row(UiKit.Label("加载器"), _loader, UiKit.Label("加载器版本"), _loaderVersion),
            UiKit.Row(UiKit.PrimaryButton("下载 / 安装", (_, _) => InstallSelected()),
                UiKit.Button("打开 versions 目录", (_, _) => MainWindow.OpenFolder(Config.VersionsDir))),
            _installStatus));

        var shopPanel = new StackPanel();
        shopPanel.Children.Add(UiKit.Card(_shopTitle,
            UiKit.Row(_query, UiKit.Label("MC 版本"), _targetVersion, UiKit.Label("加载器"), _modLoader),
            UiKit.Row(UiKit.PrimaryButton("搜索", (_, _) => SearchShop()),
                UiKit.Button("清空", (_, _) => _results.ItemsSource = null)),
            _searchStatus,
            _results,
            UiKit.Note("双击结果即可下载到「MC 版本」对应实例；整合包会按索引下载文件并解压 overrides。")));

        var installedPanel = new StackPanel();
        var installButtons = UiKit.Row(
            UiKit.Button("刷新", (_, _) => Refresh()),
            UiKit.Button("打开目录", (_, _) => MainWindow.OpenFolder(Config.VersionsDir)),
            UiKit.Button("再装一次 / 修复", (_, _) => RepairSelected()),
            UiKit.Button("删除版本", (_, _) => DeleteSelected()));
        installedPanel.Children.Add(UiKit.Card("已安装版本", _installed, _installedInfo, installButtons));
        _installed.SelectionChanged += (_, _) =>
        {
            if (_installed.SelectedItem is string id)
            {
                var detail = VersionService.Inspect(id);
                _installedInfo.Text = $"{id} · {detail.Loader} · {detail.Type} · {detail.Size / 1048576.0:F1} MB" +
                                      (detail.Inherits.Length > 0 ? $" · 继承 {detail.Inherits}" : "");
            }
        };

        _panes[0] = gamePanel;
        _panes[1] = shopPanel;
        _panes[2] = installedPanel;
        for (var i = 0; i < _panes.Length; i++)
        {
            _panes[i].Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            _host.Children.Add(_panes[i]);
        }

        _categories.Items.Add("游戏版本");
        _categories.Items.Add("整合包");
        _categories.Items.Add("模组");
        _categories.Items.Add("资源包");
        _categories.Items.Add("光影");
        _categories.Items.Add("已安装");
        _categories.SelectedIndex = 0;
        _categories.SelectionChanged += (_, _) => SwitchCategory();

        var left = new StackPanel();
        left.Children.Add(UiKit.Heading("下载分类"));
        left.Children.Add(_categories);
        left.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
        left.Children.Add(UiKit.Note("模组 / 资源包 / 整合包来自 Modrinth。"));
        left.Children.Add(UiKit.Note("国内下载游戏本体建议用 BMCLAPI 镜像。"));

        return UiKit.TwoPane(left, _host);
    }

    private void SwitchCategory()
    {
        var index = _categories.SelectedIndex;
        var pane = index switch
        {
            5 => 2,
            >= 1 and <= 4 => 1,
            _ => 0
        };
        for (var i = 0; i < _panes.Length; i++)
            _panes[i].Visibility = i == pane ? Visibility.Visible : Visibility.Collapsed;

        _shopType = index switch
        {
            1 => "modpack",
            2 => "mod",
            3 => "resourcepack",
            4 => "shader",
            _ => "mod"
        };
        if (index >= 1 && index <= 4)
            _shopTitle.Text = "Modrinth · " + _categories.SelectedItem;
        if (index == 5) Refresh();
    }

    public void Refresh()
    {
        var installed = VersionService.Installed();
        _installed.ItemsSource = installed;
        if (installed.Count == 0) _installedInfo.Text = "还没有安装任何版本";
        if (_targetVersion.Text.Length == 0 && installed.Count > 0) _targetVersion.Text = installed[0];
    }

    private void FetchVersions()
    {
        _shell.Status("获取版本列表中...");
        _shell.RunAsync(() =>
        {
            VersionService.Fetch(true);
            _shell.Dispatcher.BeginInvoke(new Action(() => RefreshVersions()));
        });
    }

    private void RefreshVersions()
    {
        List<VersionService.Entry> list;
        try
        {
            list = VersionService.Fetch();
        }
        catch (Exception ex)
        {
            _shell.Log("版本列表获取失败：" + ex.Message);
            return;
        }

        var filter = _versionFilter.SelectedIndex;
        var keyword = _versionSearch.Text.Trim();
        var filtered = list.Where(v =>
            filter switch
            {
                1 => v.Type == "release",
                2 => v.Type == "snapshot",
                _ => true
            });
        if (keyword.Length > 0)
            filtered = filtered.Where(v => v.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        var items = filtered.Select(v => $"{v.Id}   [{v.Type}]   {v.Time[..Math.Min(10, v.Time.Length)]}").ToList();
        _versions.ItemsSource = items;
        if (items.Count > 0) _versions.SelectedIndex = 0;
        _installStatus.Text = $"共 {items.Count} 个版本";
    }

    private void FillLoaderVersions()
    {
        var id = SelectedVersionId();
        _loaderVersion.Items.Clear();
        if (id == null) return;
        var loader = _loader.SelectedItem as string ?? "原版";
        if (loader.StartsWith("原版") || loader == "OptiFine") return;

        _shell.RunAsync(() =>
        {
            try
            {
                var list = loader switch
                {
                    "Fabric" => LoaderInstaller.FabricLoaders(id),
                    "Quilt" => LoaderInstaller.QuiltLoaders(id),
                    "Forge" => LoaderInstaller.ForgeVersions(id),
                    _ => new List<string>()
                };
                _shell.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _loaderVersion.Items.Clear();
                    foreach (var v in list) _loaderVersion.Items.Add(v);
                    if (list.Count > 0) _loaderVersion.Text = list[0];
                    _installStatus.Text = list.Count > 0
                        ? $"找到 {list.Count} 个 {loader} 加载器版本"
                        : "该加载器需要手动填写版本号";
                }));
            }
            catch (Exception ex)
            {
                _shell.Dispatcher.BeginInvoke(new Action(() => _installStatus.Text = ex.Message));
            }
        });
    }

    private string SelectedVersionId()
    {
        if (_versions.SelectedItem is not string raw) return null;
        var id = raw.Split("   ")[0].Trim();
        return id.Length > 0 ? id : null;
    }

    private void InstallSelected()
    {
        var id = SelectedVersionId();
        if (id == null)
        {
            _shell.Status("先在版本列表里选一个版本");
            return;
        }
        var loader = _loader.SelectedItem as string ?? "原版（无加载器）";
        var loaderVersion = _loaderVersion.Text.Trim();
        var entry = VersionService.Fetch().FirstOrDefault(e => e.Id == id);

        _shell.Status($"开始安装 {id}");
        _shell.RunAsync(() =>
        {
            VersionService.Install(id, entry?.Url ?? Config.MetaBase + $"mc/game/version/{id}/json",
                (text, progress) => _shell.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _shell.SetProgress(progress, text);
                    if (progress >= 1) _installStatus.Text = text;
                })));

            if (!loader.StartsWith("原版"))
            {
                var installed = InstallLoader(loader, id, loaderVersion);
                _shell.Dispatcher.BeginInvoke(new Action(() =>
                    _shell.Status($"安装完成：{id} + {loader} → {installed}")));
            }
            else
            {
                _shell.Dispatcher.BeginInvoke(new Action(() => _shell.Status("安装完成：" + id)));
            }
            _shell.Dispatcher.BeginInvoke(new Action(() => _shell.SetProgress(0)));
        });
    }

    private string InstallLoader(string loader, string mc, string loaderVersion)
    {
        switch (loader)
        {
            case "Fabric":
            {
                var list = LoaderInstaller.FabricLoaders(mc);
                var pick = list.Contains(loaderVersion) ? loaderVersion : list[0];
                return LoaderInstaller.InstallFabric(mc, pick);
            }
            case "Quilt":
            {
                var list = LoaderInstaller.QuiltLoaders(mc);
                var pick = list.Contains(loaderVersion) ? loaderVersion : list[0];
                return LoaderInstaller.InstallQuilt(mc, pick);
            }
            case "Forge":
            case "NeoForge":
            {
                if (loaderVersion.Length == 0) throw new Exception("请先填写或选择加载器版本");
                var kind = loader == "Forge" ? LoaderInstaller.Kind.Forge : LoaderInstaller.Kind.NeoForge;
                return LoaderInstaller.InstallInstallerBased(kind, mc, loaderVersion, s => _shell.Log(s));
            }
            case "OptiFine":
            {
                if (loaderVersion.Length == 0) throw new Exception("OptiFine 需要填写版本号，例如 HD_U_I5");
                return LoaderInstaller.InstallOptiFine(mc, loaderVersion, s => _shell.Log(s));
            }
            default:
                return mc;
        }
    }

    private void SearchShop()
    {
        var query = _query.Text.Trim();
        var target = _targetVersion.Text.Trim();
        var loader = _modLoader.SelectedItem as string;
        var type = _shopType;
        _searchStatus.Text = "搜索中...";
        _shell.RunAsync(() =>
        {
            var hits = Modrinth.Search(query, type, target, type == "mod" ? loader : null);
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                _results.ItemsSource = hits
                    .Select(h => $"{h.Title}   ↓{h.Downloads}   @{h.Author}   #{h.Slug}")
                    .ToList();
                _searchStatus.Text = $"找到 {hits.Count} 个结果，双击安装";
            }));
        });
    }

    private void InstallFromShop()
    {
        if (_results.SelectedItem is not string item) return;
        var slug = item[(item.LastIndexOf('#') + 1)..].Trim();
        var target = _targetVersion.Text.Trim();
        if (target.Length == 0)
        {
            _searchStatus.Text = "先填写目标 MC 版本";
            return;
        }
        var loader = _modLoader.SelectedItem as string;
        var type = _shopType;
        _searchStatus.Text = "下载中：" + slug;
        _shell.RunAsync(() =>
        {
            if (type == "modpack")
                Modrinth.InstallModpack(slug, target, target, s => _shell.Log(s));
            else
                Modrinth.Install(slug, type, target, type == "mod" ? loader : null, target, s => _shell.Log(s));
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                _searchStatus.Text = "安装完成：" + slug;
                _shell.Status("安装完成：" + slug);
            }));
        });
    }

    private void RepairSelected()
    {
        if (_installed.SelectedItem is not string id) return;
        var entry = VersionService.Fetch().FirstOrDefault(e => e.Id == id);
        if (entry == null)
        {
            _shell.Status("该版本不是原版版本（可能是加载器版本），请在「游戏版本」分类里重新安装基础版本");
            return;
        }
        _shell.Status("修复 " + id);
        _shell.RunAsync(() =>
        {
            VersionService.Install(id, entry.Url, (text, progress) =>
            {
                _shell.Dispatcher.BeginInvoke(new Action(() => _shell.SetProgress(progress, text)));
            });
            _shell.Dispatcher.BeginInvoke(new Action(() =>
            {
                _shell.SetProgress(0);
                _shell.Status("修复完成：" + id);
            }));
        });
    }

    private void DeleteSelected()
    {
        if (_installed.SelectedItem is not string id) return;
        if (MessageBox.Show($"删除版本 {id}？", "PCLGE", MessageBoxButton.OKCancel,
                MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        try
        {
            VersionService.Delete(id);
            Refresh();
            _shell.Status("已删除：" + id);
        }
        catch (Exception ex)
        {
            _shell.Status("删除失败：" + ex.Message);
        }
    }
}
