using System;
using System.Windows.Forms;
using Sifon.Forms.Initialize;
using Sifon.Forms.MainForm;
using Sifon.Shared.Exceptions;
using Sifon.Shared.Extensions;
using Sifon.Shared.Logger;
using Sifon.Shared.Providers.Profile;
using Sifon.Shared.Validation;
using Sifon.Statics;

namespace Sifon
{
    static class Program
    {
        private static readonly IFormValidation FormValidation = new FormValidation();

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var onStart = new OnStart();
            if (onStart.EnsureAdminRights() && onStart.IsValid)
            {
                try
                {
                    onStart.EnableLogger();
                    SimpleLog.Info("Sifon started.");

                    Application.Run(new Main());
                }
                catch (Exception e)
                {
                    if (e.InnerException is RemoteNotInitializedException)
                    {
                        
                        if (FormValidation.ShowYesNo(Messages.General.YesNoCaption, Messages.Program.NoRemoteFolder))
                        {
                            var initializeForm = new InitRemote
                            {
                                StartPosition = FormStartPosition.CenterParent,
                                RemoteSettings = new ProfilesProvider().SelectedProfile

                            };
                            if (initializeForm.ShowDialog() == DialogResult.OK && initializeForm.RemoteFolder.NotEmpty())
                            {
                                initializeForm.Dispose();
                                Application.Run(new Main());
                            }
                            else
                            {
                                FormValidation.ShowError(Messages.Program.InitializeFailure, Messages.Program.CannotContinue);
                            }
                        }
                        else
                        {
                            FormValidation.ShowError(Messages.Program.RemoteExecutionError, Messages.Program.CannotContinue);
                        }
                    }
                    else
                    {
                        FormValidation.ShowError(Messages.Program.ExecutionError, Messages.Program.CannotContinue);
                    }

                    SimpleLog.Log(e);
                }
            }
        }
    }
}