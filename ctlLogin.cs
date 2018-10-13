using System;
using System.Diagnostics;
using System.Windows.Forms;
using WTT.BitmexDataFeed.Properties;

namespace WTT.BitmexDataFeed
{
    public partial class ctlLogin : UserControl
    {
        public ctlLogin()
        {
            InitializeComponent();
        }

        public string ApiKey => txtApiKey.Text.Trim();

        public string Secret => txtSecret.Text.Trim();

        private void ctlLogin_Load(object sender, EventArgs e)
        {
            txtApiKey.Text = Settings.Default.ApiKey;

            txtSecret.Text = Settings.Default.Secret;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Navigate to a URL.
            Process.Start("https://www.bitmex.com/app/apiOverview");
        }

    }
}