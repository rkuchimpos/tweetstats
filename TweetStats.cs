using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using CsQuery;

// Build and run with: 
// csc TweetStats.cs /reference:CsQuery.dll && start TweetStats
public class TweetStatsRunner
{
	/*
	Example output:
	Short Tweets Sample Size: 50
	        Mean Engagements: 3823.28
	                 Std Dev: 8512.51619684995
	 Long Tweets Sample Size: 50
	        Mean Engagements: 1799.86
	                 Std Dev: 3613.05762314459
	*/
	public static void Main()
	{
		List<Tweet> tweets = new TwitterService().GetTweets(/* HTML DATA SOURCE */);
		List<Tweet> shortTweetsArchive = tweets.Where(t => t.Content.Length < 100).ToList();
		List<Tweet> longTweetsArchive = tweets.Where(t => t.Content.Length >= 100).ToList();

		List<Tweet> shortTweetsSample = new List<Tweet>();
		List<Tweet> longTweetsSample = new List<Tweet>();

		Random rand = new Random();
		for (int i = 0; i < 50; i++) {
			int r = rand.Next(shortTweetsArchive.Count);
			shortTweetsSample.Add(shortTweetsArchive[r]);
			shortTweetsArchive.RemoveAt(r); // Sampling without replacement
		}

		for (int i = 0; i < 50; i++) {
			int r = rand.Next(longTweetsArchive.Count);
			longTweetsSample.Add(longTweetsArchive[r]);
			longTweetsArchive.RemoveAt(r);
		}

		Func<Tweet, int> getEngagements = t => (t.RetweetCount + t.FavoriteCount);
		IEnumerable<int> shortTweetsSampleData = shortTweetsSample.Select(getEngagements);
		IEnumerable<int> longTweetsSampleData = longTweetsSample.Select(getEngagements);

		double shortAvg = shortTweetsSampleData.Average();
		double shortSD = shortTweetsSampleData.StandardDeviation();
		double longAvg = longTweetsSampleData.Average();
		double longSD = longTweetsSampleData.StandardDeviation();

		Console.WriteLine("Short Tweets Sample Size: {0}", shortTweetsSample.Count);
		Console.WriteLine("        Mean Engagements: {0}", shortAvg);
		Console.WriteLine("                 Std Dev: {0}", shortSD);
		Console.WriteLine(" Long Tweets Sample Size: {0}", longTweetsSample.Count);
		Console.WriteLine("        Mean Engagements: {0}", longAvg);
		Console.WriteLine("                 Std Dev: {0}", longSD);


		IOUtils.Export(shortTweetsSampleData, /* OUTPUT LOCATION */);
		IOUtils.Export(longTweetsSampleData, /* OUTPUT LOCATION */);

		Console.WriteLine("* Done");
		Console.ReadKey();
	}
}

public class Tweet
{
	public string ID { get; set; }
	public string Author { get; set; }
	public string PublishDate { get; set; }
	public string Content { get; set; }
	public int RetweetCount { get; set; }
	public int FavoriteCount { get; set; }
}

public class TwitterService
{
	private const string userAgent = "Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0.2623.110 Safari/537.36";

	// TODO: Add capability of specifying number of tweets to pull
	// NOTE: Scraping: Violates Twitter's Terms of Service?
	private string DownloadPage(string url = "http://m.twitter.com")
	{
		WebClient wc = new WebClient();
		wc.Headers.Add("user-agent", userAgent);
		return wc.DownloadString(url);
	}

	private string LoadPageFromFile(string filepath)
	{
		string content = File.ReadAllText(filepath);
		return content;
	}

	// Source can be a URL or an HTML file
	public List<Tweet> GetTweets(string source)
	{
		string res = null;

		if (source.ToLower().Contains("twitter.com"))
		{
			res = DownloadPage(source);
		}
		else if (source.ToLower().EndsWith(".html"))
		{
			res = LoadPageFromFile(source);
		}
		else
		{
			throw new ArgumentException("Invalid source.");
		}

		CQ dom = CQ.Create(res);
		CQ timeline = dom["div.Timeline-item[role='row']:not([jsaction='moreClick']) > div[role='gridcell']"];

		List<Tweet> tweets = new List<Tweet>();
		foreach (var timelineItem in timeline)
		{
			var item = CQ.Create(timelineItem);

			Tweet t = new Tweet()
			{
				ID = item.Find("div.Tweet").Attr("data-tweet-id"),
				Author = item.Find("span.UserNames-screenName").Text(),
				PublishDate = item.Find("a.Tweet-timestamp > time").Attr("datetime"),
				Content = item.Find("div.Tweet-text").Text(),
				RetweetCount = ParseNumber(item.Find("span.TweetAction-count").Eq(0).Text()),
				FavoriteCount = ParseNumber(item.Find("span.TweetAction-count").Eq(1).Text())
			};

			tweets.Add(t);
		}

		return tweets;
	}

	// Converts representations of numbers like 13.5K to 135000
	// and 1,234 to 1234
	private int ParseNumber(string numString)
	{
		int num = -1;
		if (numString.EndsWith("K"))
		{
			numString = numString.Replace("K", "");
			int dotIdx = numString.IndexOf(".");
			if (dotIdx > -1)
			{
				int thousands = Convert.ToInt32(numString.Substring(0, dotIdx));
				int hundreds = Convert.ToInt32(numString.Substring(dotIdx + 1));
				num = thousands * 1000 + hundreds * 100;
			}
			else
			{
				int thousands = Convert.ToInt32(numString);
				num = thousands * 1000;
			}
		}
		else if (numString.Contains(","))
		{
			num = Convert.ToInt32(numString.Replace(",", ""));
		}
		else
		{
			num = Convert.ToInt32(numString);
		}

		return num;
	}
}

public static class LinqExtensions
{
	// Sample Standard Deviation
	public static double StandardDeviation(this IEnumerable<int> list)
	{
		int n = list.Count();
		double avg = list.Average();
		double s = 0;

		foreach (double i in list)
		{
			double diff = i - avg;
			s += diff * diff;
		}

		double stdDev = Math.Sqrt((1.0 / (n - 1)) * s);
		return stdDev;
	}
}

public static class IOUtils
{	
	public static void Export(IEnumerable<int> source, string path)
	{
		string data = String.Join("\n", source);
		// Assume file does not already exist
		File.WriteAllText(path, data);
	}
}