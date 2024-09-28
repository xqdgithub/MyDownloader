using AntdUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyDownloader.UserControls
{
    public partial class SearchInput : UserControl
    {
        public Input InputControl { get { return input1; } }
        public AntdUI.Button ButtonControl { get { return button1; } }

        public ClickEventHandler ButtonClick;
       

        public SearchInput()
        {
            InitializeComponent();
        }
    }
}
