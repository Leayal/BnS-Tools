using System;
using System.Drawing;
using System.Windows.Forms;

namespace BnSLaunchWrapper
{
    class WrapperForm : Form
    {
        public WrapperForm() : base() { this.Icon = Properties.Resources.icon; this.Opacity = 0; this.FormBorderStyle = FormBorderStyle.None; this.BackColor = Color.White; this.TransparencyKey = Color.White; }
    }
}
