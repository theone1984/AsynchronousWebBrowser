/* Asynchronous WebBrowser
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
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AsynchronousWebBrowser
{
    public class AsynchronousWebBrowser
    {
        private delegate void WebBrowserNavigateHandler(String url);

        private const int webBrowserMargin = 10;
        private const int imageMarginTop = 4;

        private WebBrowser webBrowser = null;
        private Form synchronizer = null;

        private volatile bool waitingForHtml = false;
        private volatile bool waitingForScreenshot = false;

        private volatile Bitmap currentScreenshot;
        private volatile String currentHtmlSource;

        private Object lockObject = new Object();

        private volatile Exception caughtException = null;

        public AsynchronousWebBrowser(Form synchronizer)
        {
            if (synchronizer == null)
            {
                throw new Exception("A synchronizer must be specified");
            }

            this.synchronizer = synchronizer;
            InitWebBrowser();
        }

        private void InitWebBrowser()
        {
            webBrowser = new WebBrowser();
            webBrowser.ScrollBarsEnabled = false;
            webBrowser.ScriptErrorsSuppressed = true;

            AttachWebBrowserEvents();
        }

        public void Dispose()
        {
            waitingForHtml = false;
            waitingForScreenshot = false;

            if (webBrowser != null)
            {
                webBrowser.Dispose();
            }
        }

        // Async methods

        public String GetHtmlSource(String url)
        {
            BusyWaitForWebBrowserToFinish();

            InitWaitForHtml();
            CallNavigate(url);

            BusyWaitForWebBrowserToFinish();
            CheckForExceptions();

            Thread.Sleep(500);

            return currentHtmlSource;
        }

        private void InitWaitForHtml()
        {
            ClearPreviousResults();
            waitingForHtml = true;
        }

        public Bitmap GetScreenshot(String url)
        {
            BusyWaitForWebBrowserToFinish();

            InitWaitForScreenshot();
            CallNavigate(url);

            BusyWaitForWebBrowserToFinish();
            CheckForExceptions();

            Thread.Sleep(500);

            return CutOffWebBrowserMargin(currentScreenshot);
        }

        private Bitmap CutOffWebBrowserMargin(Bitmap screenshot)
        {
            Rectangle cutRectangle = new Rectangle(webBrowserMargin, webBrowserMargin, screenshot.Width - webBrowserMargin, screenshot.Height - webBrowserMargin);

            Bitmap cutScreenshot = new Bitmap(cutRectangle.Width, cutRectangle.Height);
            Graphics graphics = Graphics.FromImage(cutScreenshot);
            graphics.DrawImage(screenshot, new Rectangle(0, 0, cutRectangle.Width, cutRectangle.Height), cutRectangle, GraphicsUnit.Pixel);

            return cutScreenshot;
        }

        private void InitWaitForScreenshot()
        {
            ClearPreviousResults();
            waitingForScreenshot = true;
        }

        private void ClearPreviousResults()
        {
            waitingForHtml = false;
            waitingForScreenshot = false;

            currentHtmlSource = null;
            currentScreenshot = null;
        }

        public void GenerateScreenshot(String url)
        {
            CallNavigate(url);
        }

        private void CallNavigate(String url)
        {
            synchronizer.BeginInvoke(new WebBrowserNavigateHandler(NavigateToUrl), url);
        }

        private void BusyWaitForWebBrowserToFinish()
        {
            while (waitingForScreenshot || waitingForHtml)
            {
                Thread.Sleep(150);
            }
        }

        private void CheckForExceptions()
        {
            if (caughtException != null)
            {
                throw caughtException;
            }
        }

        // Synchronized methods

        private void NavigateToUrl(String url)
        {
            try
            {
                webBrowser.Stop();
                webBrowser.Navigate(url);
                AttachWebBrowserEvents();
            }
            catch (Exception e)
            {
                StopWaiting(e);
            }
        }

        private void AttachWebBrowserEvents()
        {
            webBrowser.ProgressChanged += new WebBrowserProgressChangedEventHandler(webBrowser_ProgressChanged);
            webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);
        }

        private void webBrowser_ProgressChanged(object sender, WebBrowserProgressChangedEventArgs args)
        {
            //Console.WriteLine(args.CurrentProgress + " of " + args.MaximumProgress + " bytes downloaded");
        }

        private void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs args)
        {
            if (webBrowser.ReadyState == WebBrowserReadyState.Complete)
            {
                DetermineWebBrowserHtmlSourceIfWaiting();
                DetermineWebBrowserScreenshotIfWaiting();
            }
        }

        private void DetermineWebBrowserHtmlSourceIfWaiting()
        {
            try
            {
                if (waitingForHtml)
                {
                    currentHtmlSource = GetWebBrowserHtmlSource();
                    StopWaiting();
                }
            }
            catch (Exception e)
            {
                StopWaiting(e);
            }
        }

        private String GetWebBrowserHtmlSource()
        {
            HtmlElementCollection htmlRootElements = webBrowser.Document.GetElementsByTagName("HTML");
            if (htmlRootElements.Count != 1)
            {
                throw new Exception("There were was not exactly one root node element");
            }

            HtmlElement htmlRootElement = htmlRootElements[0];
            return htmlRootElement.OuterHtml;
        }

        private void DetermineWebBrowserScreenshotIfWaiting()
        {
            try
            {
                if (waitingForScreenshot)
                {
                    currentScreenshot = TakeWebBrowserScreenshot();
                    StopWaiting();
                }
            }
            catch (Exception e)
            {
                StopWaiting(e);
            }
        }

        private Bitmap TakeWebBrowserScreenshot()
        {
            if (IsImageDocument())
                SetSizeAccordingToImage();
            else
                SetSizeAccordingToBody();

            Bitmap screenshot = new Bitmap(webBrowser.Width, webBrowser.Height);
            Rectangle screenRectangle = new Rectangle(0, 0, webBrowser.Width, webBrowser.Height);
            webBrowser.DrawToBitmap(screenshot, screenRectangle);

            if (IsImageDocument())
            {
                screenshot = CutImageDocumentMargin(screenshot);
            }

            return screenshot;
        }

        private Bitmap CutImageDocumentMargin(Bitmap screenshot)
        {
            Bitmap cutScreenshot = new Bitmap(screenshot.Width, screenshot.Height - webBrowserMargin);
            Graphics graphics = Graphics.FromImage(cutScreenshot);

            graphics.DrawImage(screenshot, new Rectangle(0, 0, cutScreenshot.Width, cutScreenshot.Height),
                                           new Rectangle(0, webBrowserMargin, screenshot.Width, screenshot.Height - webBrowserMargin), GraphicsUnit.Pixel);

            return cutScreenshot;
        }

        private bool IsImageDocument()
        {
            return (webBrowser.Document.Body.All.Count == 1 && webBrowser.Document.Images.Count == 1);
        }

        private void SetSizeAccordingToBody()
        {
            webBrowser.Width = webBrowser.Document.Body.ScrollRectangle.Width + webBrowserMargin;
            webBrowser.Height = webBrowser.Document.Body.ScrollRectangle.Height + webBrowserMargin;
        }

        private void SetSizeAccordingToImage()
        {
            webBrowser.Width = webBrowser.Document.Images[0].ScrollRectangle.Width + webBrowserMargin;
            webBrowser.Height = webBrowser.Document.Images[0].ScrollRectangle.Height + webBrowserMargin;
        }

        private void StopWaiting(Exception e)
        {
            caughtException = e;
            StopWaiting();
        }

        private void StopWaiting()
        {
            DetachWebBrowserEvents();
            webBrowser.Stop();

            waitingForHtml = false;
            waitingForScreenshot = false;
        }

        private void DetachWebBrowserEvents()
        {
            webBrowser.ProgressChanged += new WebBrowserProgressChangedEventHandler(webBrowser_ProgressChanged);
            webBrowser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(webBrowser_DocumentCompleted);
        }
    }
}
