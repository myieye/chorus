﻿using System;
using System.Drawing;
using System.Windows.Forms;
using Chorus.Utilities;
using Chorus.VcsDrivers.Mercurial;
using Palaso.Code;

namespace Chorus.UI.Misc
{
	/// <summary>
	/// Show this control somewhere in the setup UI of your application.
	/// It will help people know if the project is ready to use LanguageDepot, and
	/// gives them a button to edit the relevant settings.
	/// </summary>
	public partial class ReadinessPanel : UserControl
	{
		public ReadinessPanel()
		{
			InitializeComponent();
			BorderStyle = System.Windows.Forms.BorderStyle.None;//having some trouble with this
		}

		/// <summary>
		/// This must be set by the client before this control is displayed
		/// </summary>
		public string ProjectFolderPath { get; set; }

		private void ReadinessPanel_Resize(object sender, EventArgs e)
		{
			_chorusReadinessMessage.MaximumSize = new Size(this.Width -(10+ _chorusReadinessMessage.Left), 0);
		}

		private void ReadinessPanel_Load(object sender, EventArgs e)
		{
			BackColor = Parent.BackColor;
			RequireThat.Directory(ProjectFolderPath).Exists();
			var repo = new HgRepository(ProjectFolderPath, new NullProgress());
			string message;
			var ready = repo.GetIsReadyForInternetSendReceive(out message);
			_warningImage.Visible = !ready;
			_chorusReadinessMessage.Text = message;
		}

		private void _editServerInfoButton_Click(object sender, EventArgs e)
		{
			var model = new ServerSettingsModel();
			model.InitFromProjectPath(ProjectFolderPath);
			using (var dlg = new ServerSettingsDialog(model))
			{
				dlg.ShowDialog();
			}
		}
	}
}
