namespace Pclge;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            Core.LogService.Write("未处理异常：" + e.Exception);
            System.Windows.MessageBox.Show("PCLGE 出现异常：\n" + e.Exception.Message, "PCLGE",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Core.LogService.Write("崩溃：" + e.ExceptionObject);
    }
}
