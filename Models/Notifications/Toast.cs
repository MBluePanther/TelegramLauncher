namespace TelegramLauncher.Notifications
{


    public class Toast
    {
        public string Message { get; set; } = string.Empty;
        public ToastKind Kind { get; set; } = ToastKind.Info;
    }
}