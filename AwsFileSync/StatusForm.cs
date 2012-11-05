using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AwsFileSync
{
    public partial class StatusForm : Form
    {
        public StatusForm()
        {
            InitializeComponent();
        }

        public string Status
        {
            get { return txtStatus == null || txtStatus.Text == null ? "" : txtStatus.Text; }
            set
            {
                if (txtStatus != null)
                {
                    txtStatus.Text = value;
                    txtStatus.SelectionLength = 0;
                }
            }
        }
    }
}
