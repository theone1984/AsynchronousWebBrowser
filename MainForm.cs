﻿/* Asynchronous WebBrowser
 * Copyright (C) 2010 Thomas Endres
 * 
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 3 of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License along with this program; if not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AsynchronousWebBrowser
{
    public partial class MainForm : Form
    {
        private delegate void ScreenshotReceivedHandler(object sender, Bitmap screenshot);

        private AsynchronousWebBrowser asyncWebBrowser;
        private Thread getScreenshotThread;

        public MainForm()
        {
            InitializeComponent();

            asyncWebBrowser = WebBrowserFactory.CreateWebBrowser(this);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            CreateThread();
        }

        private void CreateThread()
        {
            getScreenshotThread = new Thread(new ThreadStart(RunThread));
            getScreenshotThread.Start();
        }

        private void RunThread()
        {
            try
            {
                Bitmap screenshot = asyncWebBrowser.GetScreenshot("http://www.google.com");
                this.BeginInvoke(new ScreenshotReceivedHandler(asyncWebBrowser_ScreenshotReceived), this, screenshot);
            }
            catch (Exception e)
            {
                Console.WriteLine("Got exception: " + e.Message);
            }
        }

        private void asyncWebBrowser_ScreenshotReceived(object sender, Bitmap screenshot)
        {
            ShowScreenshot(screenshot);
        }

        private void ShowScreenshot(Bitmap screenshot)
        {
            pictureBoxScreenshot.Image = screenshot;
        }
    }
}
