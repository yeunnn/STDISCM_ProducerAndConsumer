// Argamosa, Daniel Cedric (S14)
// Donato, Adriel Joseph (S12)

using System.Text;

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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Show the configuration dialog to gather user settings.
            ConfigForm configForm = new ConfigForm();
            if (configForm.ShowDialog() == DialogResult.OK)
            {
                // Validate the inputs before proceeding
                string validationMessage = ValidateInputs(configForm);
                if (string.IsNullOrEmpty(validationMessage))
                {
                    Application.Run(new MainForm(configForm.ConsumerThreadsCount, configForm.QueueCapacity, configForm.ListeningPort));
                }
                else
                {
                    MessageBox.Show(validationMessage, "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /*
        * Validates the inputs from the ConfigForm to ensure they meet the required criteria
        *
        * @param configForm - The configuration form containing the user inputs
        *
        * @return - A string with validation error messages, or an empty string if the inputs are valid
        */
        static string ValidateInputs(ConfigForm configForm)
        {
            StringBuilder errorMessage = new StringBuilder();

            if (configForm.ConsumerThreadsCount == 0 || configForm.QueueCapacity == 0 || configForm.ListeningPort == 0)
            {
                errorMessage.AppendLine("None of the inputs can be zero or a character");
            }
            else
            {
                // Validate ConsumerThreadsCount (must be positive)
                if (configForm.ConsumerThreadsCount <= 0)
                {
                    errorMessage.AppendLine("Consumer Threads Count must be a positive number");
                }

                // Validate QueueCapacity (must be positive)
                if (configForm.QueueCapacity <= 0)
                {
                    errorMessage.AppendLine("Queue Capacity must be a positive number");
                }

                // Validate ListeningPort (must be a valid port number, between 1 and 65535)
                if (configForm.ListeningPort < 1 || configForm.ListeningPort > 65535)
                {
                    errorMessage.AppendLine("Listening Port must be a valid port number between 1 and 65535");
                }
            }

            // If no validation errors, return an empty string
            return errorMessage.ToString();
        }
    }
}