namespace DCPlusPlus
{
    partial class RssControl
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
            this.ItemsView = new System.Windows.Forms.ListView();
            this.columnHeader1 = new System.Windows.Forms.ColumnHeader();
            this.SuspendLayout();
            // 
            // ItemsView
            // 
            this.ItemsView.Activation = System.Windows.Forms.ItemActivation.OneClick;
            this.ItemsView.Alignment = System.Windows.Forms.ListViewAlignment.Left;
            this.ItemsView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1});
            this.ItemsView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ItemsView.FullRowSelect = true;
            this.ItemsView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.ItemsView.HotTracking = true;
            this.ItemsView.HoverSelection = true;
            this.ItemsView.LabelWrap = false;
            this.ItemsView.Location = new System.Drawing.Point(0, 0);
            this.ItemsView.MultiSelect = false;
            this.ItemsView.Name = "ItemsView";
            this.ItemsView.ShowGroups = false;
            this.ItemsView.ShowItemToolTips = true;
            this.ItemsView.Size = new System.Drawing.Size(150, 150);
            this.ItemsView.TabIndex = 0;
            this.ItemsView.UseCompatibleStateImageBehavior = false;
            this.ItemsView.View = System.Windows.Forms.View.Details;
            this.ItemsView.DoubleClick += new System.EventHandler(this.ItemsView_DoubleClick);
            this.ItemsView.Resize += new System.EventHandler(this.ItemsView_Resize);
            this.ItemsView.SelectedIndexChanged += new System.EventHandler(this.ItemsView_SelectedIndexChanged);
            this.ItemsView.Click += new System.EventHandler(this.ItemsView_Click);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "Title";
            this.columnHeader1.Width = 146;
            // 
            // RssControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.ItemsView);
            this.Name = "RssControl";
            this.Load += new System.EventHandler(this.RssControl_Load);
            this.Resize += new System.EventHandler(this.RssControl_Resize);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView ItemsView;
        private System.Windows.Forms.ColumnHeader columnHeader1;
    }
}
