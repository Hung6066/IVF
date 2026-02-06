namespace IVF.FingerprintClient;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Ensure single instance
        using var mutex = new Mutex(true, "IVF.FingerprintClient", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Ứng dụng đã đang chạy.", "IVF Fingerprint Client", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}