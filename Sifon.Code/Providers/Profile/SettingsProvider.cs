﻿using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Sifon.Abstractions.Profiles;
using Sifon.Abstractions.Providers;
using Sifon.Code.Model.Profiles;
using Sifon.Code.Statics;
using Sifon.Code.Extensions;
using Sifon.Code.Extensions.Models;

namespace Sifon.Code.Providers.Profile
{
    internal class SettingsProvider : BaseEncryptedProvider, ISettingsProvider
    {
        private ISettingRecord _entity;

        internal SettingsProvider()
        {
            _entity = Read();
        }

        #region CRUD

        public ISettingRecord Read()
        {
            _entity = new SettingRecord();

            if (File.Exists(Folders.SettingsFolder.SettingsPath))
            {
                var doc = new XmlDocument();
                doc.Load(Folders.SettingsFolder.SettingsPath);

                _entity = new SettingRecord(doc.DocumentElement);

                if (!string.IsNullOrWhiteSpace(_entity.PortalPassword))
                {
                    _entity.PortalPassword = Encryptor.Decrypt(_entity.PortalPassword);
                }
            }

            return _entity;
        }

        public void SaveCredentials(IPortalCredentials portalCredentials)
        {
            _entity.PortalUsername = portalCredentials.PortalUsername;
            _entity.PortalPassword = portalCredentials.PortalPassword;

            SaveSettings();
        }

        public void SaveCrashDetails(ICrashDetails crashDetails)
        {
            _entity.SendCrashDetails = crashDetails.SendCrashDetails;
            _entity.PluginsRepository = crashDetails.PluginsRepository;
            _entity.AlignVersions = crashDetails.AlignVersions;

            SaveSettings();
        }

        private void SaveSettings()
        {
            var doc = new XDocument();
            doc.Add(_entity.Save(Encryptor));
            doc.Save(Folders.SettingsFolder.SettingsPath);
        }

        #endregion

        public void AddScriptSettingsParameters(IDictionary<string, object> parameters)
        {
            Read();

            if (_entity.PortalUsername.NotEmpty() && _entity.PortalPassword.NotEmpty())
            {
                TestScriptSettingsParameters(parameters, _entity.PortalUsername, _entity.PortalPassword);
            }

            if (_entity.PluginsRepository.NotEmpty())
            {
                string branch = _entity.AlignVersions ? $"v{Settings.VersionNumber}" : "master";

                parameters.Add(Settings.Parameters.PluginsRepository, _entity.PluginsRepository);
                parameters.Add(Settings.Parameters.VersionBranch, branch);
            }
        }

        public void TestScriptSettingsParameters(IDictionary<string, object> parameters, string username, string password)
        {
            var credential = new System.Management.Automation.PSCredential(username, password.ToSecureString());
            parameters.Add(Settings.Parameters.PortalCredentials, credential);
        }
    }
}
