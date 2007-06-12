using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace DCPlusPlus
{
    public partial class RssControl : UserControl
    {
        private Rss Feed = new Rss();
        [Category("Appearance")]
        [Description("Gets or sets the url of the rss feed")]
        public string FeedUrl
        {
            get { return Feed.Url; }
            set { Feed.FetchFeed(value); }
        }

        private int max_items = 3;
        [Category("Appearance")]
        [Description("Gets or sets the number of feed items to show")]
        public int FeedMaxItems
        {
            get { return max_items; }
            set { 
                max_items = value; 
                Feed.FireFeedUpdated(); 
            }
        }
        

        //private string title = "";

        [Category("Appearance")]
        [Description("Gets or sets the Title Text")]
        public string Title
        {
            get 
            {

                return columnHeader1.Text; 
            }
            set 
            {
                columnHeader1.Text = value; 
            }
        }

        [Category("Appearance")]
        [Description("sets the properties of the listview")]
        public ListView FeedItemsView
        {
            get { return ItemsView; }
        }

        private void FeedUpdated(Rss feed)
        {
            ItemsView.Items.Clear();
            ListViewItem top_item = new ListViewItem(Title);
            //view_item.Text = Title;
            top_item.IndentCount = 0;
            //view_item.ToolTipText = Title;
            //view_item.ImageIndex = ;
            ItemsView.Items.Add(top_item);

            int items_num = 0;
            foreach (Rss.Channel channel in Feed.Channels)
            {
                foreach (Rss.Channel.Item item in channel.Items)
                {
                    ListViewItem view_item = new ListViewItem(item.Title);
                    //view_item.Text = item.Title;
                    view_item.Tag = item;
                    view_item.ToolTipText = item.Description;
                    view_item.ImageIndex = 1;
                    ItemsView.Items.Add(view_item);
                    items_num++;
                    if (items_num >= max_items)
                        return;
                }
            }
            //columnHeader1.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            columnHeader1.Width = -2;
        }

        public RssControl()
        {
            InitializeComponent();
            Feed.FeedUpdated += delegate(Rss feed)
            {
                if (this.InvokeRequired)
                {
                            object[] args = { feed };
                            this.Invoke(new Rss.FeedUpdatedEventHandler(FeedUpdated), args);
                }
                else FeedUpdated(feed);
            };
        }

        private void RssControl_Load(object sender, EventArgs e)
        {
            //columnHeader1.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            //Feed.FetchFeed();
        }

        private void ItemsView_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void ItemsView_DoubleClick(object sender, EventArgs e)
        {
        }

        private void ItemsView_Click(object sender, EventArgs e)
        {
            if (ItemsView.SelectedItems.Count == 1)
            {
                Rss.Channel.Item item = (Rss.Channel.Item)ItemsView.SelectedItems[0].Tag;
                //System.Diagnostics.Process.Start("IExplore", item.Link);
                try
                {
                    System.Diagnostics.Process.Start(item.Link);
                }
                catch (Exception ex)
                {
                }

            }

        }

        private void ItemsView_Resize(object sender, EventArgs e)
        {
            //columnHeader1.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            //columnHeader1.
            //columnHeader1.Width = ItemsView.Width-20;
        }

        private void RssControl_Resize(object sender, EventArgs e)
        {
            //columnHeader1.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
        }
    }
}
