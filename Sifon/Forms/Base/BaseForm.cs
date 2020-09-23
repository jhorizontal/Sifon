﻿using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sifon.Shared.Events;
using Sifon.Shared.Statics;

namespace Sifon.Forms.Base
{
    public class BaseForm : AbstractForm
    {
        public delegate Task AsyncEventHandler<T>(object sender, EventArgs e);

        public event EventHandler<EventArgs<TextBox, bool>> FolderBrowserClicked = delegate { };
        public event EventHandler<EventArgs> FormLoaded = delegate { };
        public event EventHandler<EventArgs> BeforeFormClosing = delegate { };

        public void Raise_FormLoaded()
        {
            FormLoaded.Invoke(this, new EventArgs());
        }

        public void Raise_FormClosing()
        {
            BeforeFormClosing.Invoke(this, new EventArgs());
        }
        
        protected internal void Raise_FolderBrowserClicked(TextBox textBox, bool allowCreatingNewFolders)
        {
            FolderBrowserClicked(this, new EventArgs<TextBox, bool>(textBox, allowCreatingNewFolders));
        }

        public void AppendEnvironmentToCaption(string suffix)
        {
            Text += $" - {suffix}";
        }

        protected void RevealPasswordWithinTextbox(TextBox textPassword, LinkLabel linkReveal)
        {
            RevealPasswordWithinTextbox(textPassword, linkReveal, linkReveal.Text.Contains(Settings.Labels.Reveal));
        }

        protected void RevealPasswordWithinTextbox(TextBox textPassword, LinkLabel linkReveal, bool reveal)
        {
            textPassword.PasswordChar = reveal ? new char() : '*';
            linkReveal.Text = reveal ? $"({Settings.Labels.Hide})" : $"({Settings.Labels.Reveal})";

            if (reveal)
            {
                linkReveal.Left += 9;
            }
            else
            {
                linkReveal.Left -= 9;
            }
        }
    }
}
