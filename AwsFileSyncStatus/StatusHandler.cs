using Amazon.Glacier.Transfer;
using AwsFileSyncStatus.Properties;
using AwsJob;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;

namespace AwsFileSyncStatus
{
    class StatusHandler
    {
        private HttpListenerContext context;

        public StatusHandler(HttpListenerContext context)
        {
            this.context = context;
        }

        public void run()
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(DataPath.DataFile);

            KeyValuePair<string, XsltArgumentList> xslt = getTransform(xml);
            XslCompiledTransform xsl = new XslCompiledTransform();
            using (XmlReader reader = XmlReader.Create(new StringReader(xslt.Key)))
            {
                xsl.Load(reader);
            }

            context.Response.ContentType="text/html";
            context.Response.ContentEncoding = UTF8Encoding.UTF8;
            context.Response.StatusCode = 200;
            xsl.Transform(xml, xslt.Value, context.Response.OutputStream);
            context.Response.Close();
        }

        private KeyValuePair<string, XsltArgumentList> getTransform(XmlDocument xml)
        {
            XsltArgumentList list = new XsltArgumentList();
            string reqPath = context.Request.Url.AbsolutePath;
            reqPath = reqPath.Contains('?') ? reqPath.Substring(0, reqPath.IndexOf('?')) : reqPath;
            reqPath = reqPath.Contains('#') ? reqPath.Substring(0, reqPath.IndexOf('#')) : reqPath;

            string[] elements = reqPath.Split('/');
            string text = Resources.MainPage;

            if (elements.Length > 2)
            {
                list.AddParam("vaultName", "", elements[1]);
                list.AddParam("archiveId", "", elements[2]);
                text = Resources.ArchiveDetails;
            }
            else if(elements.Length > 1)
            {
                list.AddParam("vaultName", "", elements[1]);
                text = Resources.VaultDetails;
            }

            return new KeyValuePair<string,XsltArgumentList>(text, list);
        }

        private string makeSafe(string value)
        {
            StringBuilder sb = new StringBuilder();

            foreach (char c in value)
            {
            }

            return sb.ToString();
        }
    }
}
