﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Sifon.Abstractions.Profiles;
using Sifon.Abstractions.Providers;
using Sifon.Code.Model.Profiles;
using Sifon.Code.Statics;
using Sifon.Code.Extensions.Models;

namespace Sifon.Code.Providers.Profile
{
    internal class SqlServerRecordProvider : BaseEncryptedProvider, ISqlServerRecordProvider
    {
        private IEnumerable<ISqlServerRecord> _entities;

        internal SqlServerRecordProvider()
        {
            _entities = Read();
        }

        #region CRUD

        public void Add(ISqlServerRecord record)
        {
            _entities = _entities.Append(record);
        }

        public IEnumerable<ISqlServerRecord> Read()
        {
            var items = new List<ISqlServerRecord>();

            if (File.Exists(Folders.SettingsFolder.SqlProfilesPath))
            {
                var doc = new XmlDocument();
                doc.Load(Folders.SettingsFolder.SqlProfilesPath);

                foreach (XmlNode node in doc.DocumentElement.ChildNodes)
                {
                    var record = new SqlServerRecord(node);

                    if (!string.IsNullOrWhiteSpace(record.SqlAdminPassword))
                    {
                        record.SqlAdminPassword = Encryptor.Decrypt(record.SqlAdminPassword);
                    }

                    items.Add(record);
                }
            }

            return items;
        }

        public void Save()
        {
            var doc = new XDocument();
            var root = new XElement(Xml.SqlServerRecord.NodeListName);
            doc.Add(root);

            foreach (var record in _entities)
            {
                record.SqlAdminPassword = Encryptor.Encrypt(record.SqlAdminPassword);
                root.Add(record.Save());
            }

            doc.Save(Folders.SettingsFolder.SqlProfilesPath);
        }

        public void Delete(string name)
        {
            var list = _entities.ToList();
            list.Remove(_entities.First(p => p.RecordName == name));
            _entities = list;
        }

        #endregion

        public ISqlServerRecord GetByName(string selectedServerName)
        {
            return _entities.FirstOrDefault(e => e.RecordName == selectedServerName);
        }

        public void UpdateSelected(string oldName, ISqlServerRecord record)
        {
            if (_entities.FirstOrDefault(p => p.RecordName == oldName) is SqlServerRecord selected)
            {
                selected.RecordName = record.RecordName;
                selected.SqlServer = record.SqlServer;
                selected.SqlAdminUsername = record.SqlAdminUsername;
                selected.SqlAdminPassword = record.SqlAdminPassword;
            }
        }
    }
}
