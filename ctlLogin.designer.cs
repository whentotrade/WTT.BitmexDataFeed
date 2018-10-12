namespace WTT.BitmexDataFeed
{
    partial class ctlLogin
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblToken = new System.Windows.Forms.Label();
            this.txtApiKey = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.formatLabel = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // lblToken
            // 
            this.lblToken.AutoSize = true;
            this.lblToken.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblToken.Location = new System.Drawing.Point(8, 22);
            this.lblToken.Name = "lblToken";
            this.lblToken.Size = new System.Drawing.Size(56, 17);
            this.lblToken.TabIndex = 0;
            this.lblToken.Text = "Api Key";
            // 
            // txtApiKey
            // 
            this.txtApiKey.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtApiKey.Location = new System.Drawing.Point(86, 20);
            this.txtApiKey.Margin = new System.Windows.Forms.Padding(2);
            this.txtApiKey.Name = "txtApiKey";
            this.txtApiKey.Size = new System.Drawing.Size(305, 23);
            this.txtApiKey.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(11, 62);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(380, 36);
            this.label1.TabIndex = 4;
            this.label1.Text = "This product uses the CoinApi.io API";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // linkLabel1
            // 
            this.linkLabel1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.linkLabel1.LinkArea = new System.Windows.Forms.LinkArea(81, 10);
            this.linkLabel1.LinkColor = System.Drawing.SystemColors.HotTrack;
            this.linkLabel1.Location = new System.Drawing.Point(14, 136);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(380, 36);
            this.linkLabel1.TabIndex = 5;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "By using the WTT CoinApi Crypto integration, you are agreeing to be bound by the " +
    "CoinApi.io Terms of Use.";
            this.linkLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.linkLabel1.UseCompatibleTextRendering = true;
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // formatLabel
            // 
            this.formatLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.formatLabel.LinkArea = new System.Windows.Forms.LinkArea(73, 13);
            this.formatLabel.LinkColor = System.Drawing.SystemColors.HotTrack;
            this.formatLabel.Location = new System.Drawing.Point(14, 100);
            this.formatLabel.Name = "formatLabel";
            this.formatLabel.Size = new System.Drawing.Size(380, 36);
            this.formatLabel.TabIndex = 6;
            this.formatLabel.TabStop = true;
            this.formatLabel.Text = "Symbol format: Exchange_SPOT_Asset_Quote \r\n(e.g. BITSTAMP_SPOT_BTC_USD) [Read mor" +
    "e...]";
            this.formatLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.formatLabel.UseCompatibleTextRendering = true;
            this.formatLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.formatLabel_LinkClicked);
            // 
            // ctlLogin
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.formatLabel);
            this.Controls.Add(this.linkLabel1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtApiKey);
            this.Controls.Add(this.lblToken);
            this.Name = "ctlLogin";
            this.Size = new System.Drawing.Size(413, 182);
            this.Load += new System.EventHandler(this.ctlLogin_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblToken;
        private System.Windows.Forms.TextBox txtApiKey;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.LinkLabel formatLabel;
    }
}
