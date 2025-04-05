// STDISCM Problem Set 3
// The main entry point for the application.

namespace STDISCM_ProblemSet3_Consumer
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            //ApplicationConfiguration.Initialize();
            //Application.Run(new Form1());
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Show the configuration dialog to gather user settings.
            ConfigForm configForm = new ConfigForm();
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new MainForm(configForm.ConsumerThreadsCount, configForm.QueueCapacity, configForm.ListeningPort));
            }
        }
    }
}