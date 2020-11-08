﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sifon.Abstractions.Model.BackupRestore;
using Sifon.Abstractions.Providers;
using Sifon.Forms.Base;
using Sifon.Code.BackupInfo;
using Sifon.Code.Events;
using Sifon.Code.Extensions;
using Sifon.Code.Factories;
using Sifon.Code.Filesystem;
using Sifon.Code.Metacode;
using Sifon.Code.Model;
using Sifon.Code.PowerShell;
using Sifon.Code.ScriptGenerators;
using Sifon.Code.Statics;
using Sifon.Statics;

namespace Sifon.Forms.MainForm
{
    internal class MainPresenter : ScriptablePresenter
    {
        private readonly IFilesystem _filesystem;

        internal MainPresenter(IMainView view): base(view)
        {
            if (!_profilesProvider.Any)
            {
                if (!view.ShowFirstRunDialog())
                {
                    throw new InvalidOperationException("Exited by user");
                }

                var provider = Create.New<IProfilesProvider>();
                provider.Save();

                view.ForceProfileDialogOnFirstRun();
                _profilesProvider.Read();
            }

            _filesystem = new FilesystemFactory(SelectedProfile, _view).CreateLocal();

            _view.FormLoaded += Loaded;
            _view.SelectedProfileChanged += SelectedProfileChanged;
            _view.ProfilesToolStripClicked += ProfilesToolStripClicked;
            _view.BackupToolStripClicked += BackupToolStripClicked;
            _view.RestoreToolStripClicked += RestoreToolStripClicked;
            _view.RemoveToolStripClicked += RemoveToolStripClicked;
            _view.ScriptToolStripClicked += ScriptToolStripClicked;
            
        }

        private string WinformsAssemblyLocation => typeof(Form).Assembly.Location;

        private IEnumerable<string> JustReadProfileNames => _profilesProvider.Read().Select(p => p.ProfileName);

        private void Loaded(object sender, EventArgs e)
        {
            if (SelectedProfile != null)
            {
                var profileNames = JustReadProfileNames;
                _view.LoadProfilesSelector(profileNames, SelectedProfile.ProfileName);
                _view.ToolStripsEnabled(ToolStripsEnabled(profileNames));
                _view.PopulateToolStripMenuItemWithPluginsAndScripts(GetPluginsAndScripts(Settings.Folders.Plugins), IsLocal);
            }
            else
            {
                _view.TerminateAsEmptyProfile();
            }
        }

        private async Task<string> LocalOrRemote(string script)
        {
            var _remoteScriptCopier = new RemoteScriptCopier(_profilesProvider.SelectedProfile, _view);
            return await _remoteScriptCopier.CopyIfRemote(script);
        }

        #region Backup-Remove-Restore


        private async void BackupToolStripClicked(object sender, EventArgs<IBackupRemoverViewModel> e)
        {
            model = e.Value;

            await BackupInfoExtensions.CreateBackupInfo(SelectedProfile.Website, SelectedProfile.Webroot, SelectedProfile, _view);

            if (model.XConnectFolder.NotEmpty())
            {
                await BackupInfoExtensions.CreateBackupInfo(string.Empty, model.XConnectFolder, SelectedProfile, _view);
            }

            if (model.IdentityFolder.NotEmpty())
            {
                await BackupInfoExtensions.CreateBackupInfo(string.Empty, model.IdentityFolder, SelectedProfile, _view);
            }

            if (model.HorizonFolder.NotEmpty())
            {
                await BackupInfoExtensions.CreateBackupInfo(string.Empty, model.HorizonFolder, SelectedProfile, _view);
            }

            if (model.PublishingFolder.NotEmpty())
            {
                await BackupInfoExtensions.CreateBackupInfo(string.Empty, model.PublishingFolder, SelectedProfile, _view);
            }

            if (model.CommerceSites != null && model.CommerceSites.Any())
            {
                foreach (var commerceSite in model.CommerceSites)
                {
                    await BackupInfoExtensions.CreateBackupInfo(string.Empty, commerceSite.Key, SelectedProfile, _view);
                }
            }

            var parameters = new Dictionary<string, dynamic> {{ Settings.Parameters.Activity, Messages.Activities.Backup }};
            _profilesProvider.AddScriptProfileParameters(parameters);
            _profilesProvider.AddBackupRemoveParameters(parameters, model);
            _profilesProvider.AddCommerceScriptParameters(parameters, model.CommerceSites);

            await PrepareAndStart(await LocalOrRemote(ScriptGeneratorFactory.Create(model, SelectedProfile).Script), parameters);
        }

        private async void RemoveToolStripClicked(object sender, EventArgs<IBackupRemoverViewModel> e)
        {
            var model = e.Value;

            var parameters = new Dictionary<string, dynamic> {{ Settings.Parameters.Activity, Messages.Activities.Remove }};
            _profilesProvider.AddScriptProfileParameters(parameters);
            _profilesProvider.AddBackupRemoveParameters(parameters, model);
            _profilesProvider.AddCommerceScriptParameters(parameters, model.CommerceSites);

            await PrepareAndStart(await LocalOrRemote(ScriptGeneratorFactory.Create(model, SelectedProfile).Script), parameters);
        }

        private async void RestoreToolStripClicked(object sender, EventArgs<IRestoreViewModel> e)
        {
            var model = e.Value;

            var parameters = new Dictionary<string, dynamic> {{ Settings.Parameters.Activity, Messages.Activities.Restore }};
            _profilesProvider.AddScriptProfileParameters(parameters);
            _profilesProvider.AddBackupRemoveParameters(parameters, model);
            _profilesProvider.AddRestoreParameters(parameters, model);
            _profilesProvider.AddCommerceScriptParameters(parameters, model.CommerceSites);

            await PrepareAndStart(await LocalOrRemote(ScriptGeneratorFactory.Create(model, SelectedProfile).Script), parameters);
         }

        #endregion

        public RemoteScriptCopier RemoteScriptCopier => new RemoteScriptCopier(_profilesProvider.SelectedProfile, _view);

        private async void ScriptToolStripClicked(object sender, EventArgs<string> e)
        {
            var metacode = new MetacodeHelper(e.Value);
            var parameters = new Dictionary<string, dynamic>();

            _profilesProvider.AddScriptProfileParameters(parameters);
            _settingsProvider.AddScriptSettingsParameters(parameters);
            _containersProvider.AddContainersParameters(parameters);
            await AddParametersFromMetacode(parameters, metacode);
            
            string script = await LocalOrRemote(e.Value);
            
            var scriptDependencies = metacode.IdentifyDependencies();
            foreach (string dependency in scriptDependencies)
            {
                string dependencyFile = $"{Path.GetDirectoryName(e.Value)}\\{dependency}";
                if (File.Exists(dependencyFile))
                {
                    await RemoteScriptCopier.CopyIfRemote(dependencyFile);
                }
            }

            await PrepareAndStart(script, parameters);

            _view.PluginsToolStripEnabled();
        }

        private async Task AddParametersFromMetacode(Dictionary<string, object> parameters, MetacodeHelper metacode)
        {
            var metacodeResultsDictionary = metacode.ExecuteMetacode(parameters, WinformsAssemblyLocation);

            foreach (var metadataPair in metacodeResultsDictionary)
            {
                // if we used external control to ask for file, add it then with a provided param
                if (metadataPair.Value is string && ((string)metadataPair.Value).IsValidFilePath())
                {
                    string localFile = (string)metadataPair.Value;

                    var resultedPath = await RemoteScriptCopier.CopyIfRemote(localFile);

                    parameters.Add(metadataPair.Key.Trim('$'), resultedPath);
                }
                else
                {
                    parameters.Add(metadataPair.Key.Trim('$'), metadataPair.Value);
                }
            }
        }

        public bool IsLocal => !SelectedProfile.RemotingEnabled;

        private void SelectedProfileChanged(object sender, EventArgs<string> e)
        {
            _profilesProvider.SelectProfile(e.Value);
            _view.PopulateToolStripMenuItemWithPluginsAndScripts(GetPluginsAndScripts(Settings.Folders.Plugins), IsLocal);
            _view.SetCaption(_profilesProvider.SelectedProfile.WindowCaptionSuffix);

            _view.ToolStripsEnabled(ToolStripsEnabled(JustReadProfileNames));
        }

        private void ProfilesToolStripClicked(object sender, EventArgs e)
        {
            _view.PopulateToolStripMenuItemWithPluginsAndScripts(GetPluginsAndScripts(Settings.Folders.Plugins), IsLocal);
            _view.ToolStripsEnabled(ToolStripsEnabled(JustReadProfileNames));

            if (SelectedProfile != null)
            {
                _view.LoadProfilesSelector(JustReadProfileNames, SelectedProfile.ProfileName);
            }
            else
            {
                _view.TerminateAsEmptyProfile();
            }
        }

        private bool ToolStripsEnabled(IEnumerable<string> profileNames)
        {
            return profileNames.Any()
                   && !string.IsNullOrWhiteSpace(SelectedProfile.SqlServerRecord?.RecordName)
                   && (!SelectedProfile.RemotingEnabled || SelectedProfile.RemoteFolder.NotEmpty());
        }

        protected override PluginMenuItem GetPluginsAndScripts(string baseDirectory)
        {
            var di = new DirectoryInfo(baseDirectory);

            var menuItem = new PluginMenuItem
            {
                DirectoryName = di.Name,
                DirectoryFullPath = di.FullName,
                Scripts = _filesystem.GetFiles(di.FullName, ".ps1"),
                Plugins = _filesystem.GetFiles(di.FullName, ".dll")
            };

            foreach (var chilDirectory in di.GetDirectories())
            {
                if (chilDirectory.Name == ".git")
                {
                    continue;
                }

                menuItem.Children.Add(GetPluginsAndScripts(chilDirectory.FullName));
            }

            return menuItem;
        }
    }
}
