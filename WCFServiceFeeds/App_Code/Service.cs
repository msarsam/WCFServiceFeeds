using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text.RegularExpressions;
using System.Xml;
[ServiceContract]
public class Service
{
    private string connStr = ConfigurationManager.ConnectionStrings["rss_connStr_sql"].ConnectionString;

    public class methodStatus
    {
        public bool status { get; set; }
        public string errorDesc { get; set; }
    }

    public class Status
    {
        public bool status { get; set; }
        public string detail { get; set; }
    }

    public class Source
    {
        public int ID { get; set; }
        public string name { get; set; }
        public bool isActive { get; set; }
    }

    public class Sources
    {
        public List<Source> sources = new List<Source>();
        public Status status = new Status();
    }

    public class Channel
    {
        public int ID { get; set; }
        public string name { get; set; }
        public bool isActive { get; set; }
    }

    public class Channels
    {
        public List<Channel> channels = new List<Channel>();
        public Status status = new Status();
    }

    public class RSSfeed
    {
        public string title { get; set; }
        public string link { get; set; }
        public string descrition { get; set; }
    }

    public class RSSfeeds
    {
        public List<RSSfeed> feeds = new List<RSSfeed>();
        public string sourceName { get; set; }
        public string channelName { get; set; }
    }

    public class MasterFeed
    {
        public List<RSSfeeds> all = new List<RSSfeeds>();
        public Status status = new Status();
    }

    private bool hasValue(string arg)
    {
        bool s = false;
        if (arg.Length > 0)
        {
            s = true;
        }
        return s;
    }

    private string RemoveHtmlTags(string html, bool allowHarmlessTags)
    {
        string s;
        if (html == null || html == string.Empty)
        {
            s = string.Empty;
        }

        //if (allowHarmlessTags)
        //{
        //   s =  Regex.Replace(html, "", string.Empty);
        //}
        else
        {
            s = Regex.Replace(html, "<[^>]*>", string.Empty);
        }
        return s;
    }

    public List<RSSfeed> ProcessRSSItem(string rssURL)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; /* handle request from https*/
        WebRequest wRequest = WebRequest.Create(rssURL);
        WebResponse wResponse = wRequest.GetResponse();
        Stream rssStream = wResponse.GetResponseStream();

        XmlDocument rssDoc = new XmlDocument();
        rssDoc.Load(rssStream);

        XmlNodeList rssItems = rssDoc.SelectNodes("rss/channel/item");
        List<RSSfeed> listOfFeeds = new List<RSSfeed>();

        for (int i = 0; i < 4; i++) // limit
        {
            RSSfeed feed = new RSSfeed();
            feed.title = rssItems.Item(i).SelectSingleNode("title").InnerText.ToString();
            feed.link = rssItems.Item(i).SelectSingleNode("link").InnerText.ToString();
            feed.descrition = RemoveHtmlTags(rssItems.Item(i).SelectSingleNode("description").InnerText, false).Replace("\"", "");

            listOfFeeds.Add(feed);
        }
        return listOfFeeds;
    }

    [OperationContract]
    [WebInvoke(Method = "*", BodyStyle = WebMessageBodyStyle.Wrapped, ResponseFormat = WebMessageFormat.Json)]
    public Sources getSources()
    {
        DataManager DataManager = new DataManager();
        string sqlStr = "SELECT rss_sources.srcID, rss_sources.sourceName, rss_userssources.userID " +
                        "FROM rss_sources " +
                        "LEFT OUTER JOIN rss_userssources ON (rss_userssources.srcID = rss_sources.srcID AND rss_userssources.userID = 1) " +
                        "ORDER BY rss_sources.ordr";
        SqlDataReader s = DataManager.SQLGetData(sqlStr, connStr);

        Sources sources = new Sources();
        if (s.HasRows)
        {
            while (s.Read())
            {
                {
                    Source source = new Source();
                    source.ID = Convert.ToInt32(s["srcID"]);
                    source.name = s["sourceName"].ToString();
                    source.isActive = hasValue(s["userID"].ToString());
                    sources.sources.Add(source);
                }
            }

            Status st = new Status();
            st.detail = "__success__";
            st.status = true;

            sources.status = st;
        }
        return sources;
    }

    [OperationContract]
    [WebInvoke(Method = "*", BodyStyle = WebMessageBodyStyle.Wrapped, ResponseFormat = WebMessageFormat.Json)]
    public Channels getChannels()
    {
        DataManager DataManager = new DataManager();
        string sqlStr = "SELECT rss_channels.chnlID, rss_channels.channelName, rss_userschannels.userID " +
                        "FROM rss_channels LEFT OUTER JOIN rss_userschannels ON (rss_userschannels.chnlID = rss_channels.chnlID AND rss_userschannels.userID = 1) " +
                        "WHERE rss_channels.chnlID <> 5 " +
                        "ORDER BY rss_channels.ordr";

        SqlDataReader s = DataManager.SQLGetData(sqlStr, connStr);

        Channels channels = new Channels();
        if (s.HasRows)
        {
            while (s.Read())
            {
                {
                    Channel channel = new Channel();
                    channel.ID = Convert.ToInt32(s["chnlID"]);
                    channel.name = s["channelName"].ToString();
                    channel.isActive = hasValue(s["userID"].ToString());
                    channels.channels.Add(channel);
                }
            }

            Status st = new Status();
            st.detail = "__success__";
            st.status = true;

            channels.status = st;
        }
        return channels;
    }

    [OperationContract]
    [WebInvoke(Method = "*", BodyStyle = WebMessageBodyStyle.Wrapped, ResponseFormat = WebMessageFormat.Json)]
    public MasterFeed getFeeds(string srcCollection, string chnlCollection)
    {

        string od = " rss_sources.ordr, rss_channels.ordr";

        DataManager DataManager = new DataManager();
        string sqlString = "SELECT rss_sources.sourceName, rss_channels.channelName, rss_sourceschannels.userChnlID, rss_sourceschannels.chnlURL " +
                           "FROM rss_sourceschannels " +
                           "INNER JOIN rss_sources ON(rss_sources.srcID = rss_sourceschannels.srcID) " +
                           "INNER JOIN rss_channels ON (rss_channels.chnlID = rss_sourceschannels.chnlID)  " +
                           "WHERE rss_sourceschannels.srcID IN (" + srcCollection + ") " +
                           "AND rss_sourceschannels.chnlID IN(" + chnlCollection + ") " +
                           "ORDER BY " + od;

        SqlDataReader s = DataManager.SQLGetData(sqlString, connStr);

        MasterFeed masterFeed = new MasterFeed();
        if (s.HasRows)
        {
            while (s.Read())
            {
                {
                    RSSfeeds rssfeeds = new RSSfeeds();
                    rssfeeds.sourceName = s["sourceName"].ToString();
                    rssfeeds.channelName = s["channelName"].ToString();
                    rssfeeds.feeds = ProcessRSSItem(s["chnlURL"].ToString());

                    masterFeed.all.Add(rssfeeds);

                    Status st = new Status();
                    st.detail = "__success__";
                    st.status = true;

                    masterFeed.status = st;
                }
            }
        }
        return masterFeed;
    }
}
