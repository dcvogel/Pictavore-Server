using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ASCOM.Pictavore_Server
{
    public partial class frmMain : Form
    {
        delegate void SetTextCallback(string text);

        public frmMain()
        {
            InitializeComponent();
        }

    }
}