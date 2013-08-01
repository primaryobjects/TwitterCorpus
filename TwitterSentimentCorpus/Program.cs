using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Configuration;
using CsvHelper;
using TweetSharp;
using AutoMapper;
using TwitterSentimentCorpus.Types;

namespace TwitterSentimentCorpus
{
    class Program
    {
        private static readonly string _consumerKey = ConfigurationManager.AppSettings["consumerKey"];
        private static readonly string _consumerSecret = ConfigurationManager.AppSettings["consumerSecret"];
        private static readonly string _corpusPath = "../../../data/corpus.csv"; // http://www.sananalytics.com/lab/twitter-sentiment
        private static readonly string _outputPath = "../../../data/tweets.csv";

        #region Private Helpers

        /// <summary>
        /// Loads the initial corpus data file, which contains Tweet Ids.
        /// </summary>
        /// <param name="pathName">File path to the file to open.</param>
        /// <returns>List of CorpusDataRow</returns>
        private static List<CorpusDataRow> LoadCorpus(string pathName)
        {
            List<CorpusDataRow> corpus = new List<CorpusDataRow>();

            using (FileStream f = new FileStream(pathName, FileMode.Open))
            {
                using (StreamReader streamReader = new StreamReader(f))
                {
                    using (CsvReader csvReader = new CsvReader(streamReader))
                    {
                        csvReader.Configuration.HasHeaderRecord = false;

                        while (csvReader.Read())
                        {
                            CorpusDataRow row = new CorpusDataRow();
                            int columnIndex = 0;

                            row.Keyword = csvReader.GetField(columnIndex++);

                            // Convert the first letter to uppercase.
                            string sentiment = csvReader.GetField(columnIndex++);
                            sentiment = sentiment.First().ToString().ToUpper() + String.Join("", sentiment.Skip(1));

                            row.Sentiment = (Sentiment)Enum.Parse(typeof(Sentiment), sentiment);
                            row.Id = Int64.Parse(csvReader.GetField(columnIndex++));

                            corpus.Add(row);
                        }
                    }
                }
            }

            return corpus;
        }

        /// <summary>
        /// Logs into Twitter, using OAuth. The user will need to copy/paste the Twitter auth code back into the console program.
        /// </summary>
        /// <returns>TwitterService</returns>
        private static TwitterService LoginTwitter()
        {
            // Pass your credentials to the service
            TwitterService service = new TwitterService(_consumerKey, _consumerSecret);

            // Step 1 - Retrieve an OAuth Request Token
            OAuthRequestToken requestToken = service.GetRequestToken();

            // Step 2 - Redirect to the OAuth Authorization URL
            Uri uri = service.GetAuthorizationUri(requestToken);
            Process.Start(uri.ToString());

            Console.Write("Enter Twitter auth key: ");
            string key = Console.ReadLine();

            // Step 3 - Exchange the Request Token for an Access Token
            string verifier = key; // <-- This is input into your application by your user
            OAuthAccessToken access = service.GetAccessToken(requestToken, verifier);

            // Step 4 - User authenticates using the Access Token
            service.AuthenticateWith(access.Token, access.TokenSecret);

            return service;
        }

        /// <summary>
        /// Save a CorpusDataRow object to the output file.
        /// </summary>
        /// <param name="row">CorpusDataRow (with Tweet DTO populated).</param>
        /// <param name="pathName">File path to output file to append to.</param>
        private static void SaveResult(CorpusDataRow row, string pathName)
        {
            using (FileStream f = new FileStream(pathName, FileMode.Append))
            {
                using (StreamWriter streamWriter = new StreamWriter(f))
                {
                    using (CsvWriter csvWriter = new CsvWriter(streamWriter))
                    {
                        csvWriter.WriteRecord<CorpusDataRow>(row);
                    }
                }
            }

        }

        /// <summary>
        /// Finds the index in the corpus to resume loading twitter data from. This allows resuming the load of tweets from an existing tweets.csv output file.
        /// </summary>
        /// <param name="corpus">List of CorpusDataRow</param>
        /// <param name="outputPath">Output path</param>
        /// <returns>int</returns>
        private static int GetResumeIndex(List<CorpusDataRow> corpus, string outputPath)
        {
            int index = 0;

            if (File.Exists(outputPath))
            {
                using (FileStream f = new FileStream(outputPath, FileMode.Open))
                {
                    using (StreamReader streamReader = new StreamReader(f))
                    {
                        using (CsvReader csvReader = new CsvReader(streamReader))
                        {
                            csvReader.Configuration.HasHeaderRecord = false;

                            var output = csvReader.GetRecords<CorpusDataRow>();
                            var last = output.LastOrDefault();
                            if (last != null)
                            {
                                index = corpus.FindIndex(c => c.Id == last.Id) + 1;
                                Console.WriteLine("Resume index " + index + ".");
                            }
                        }
                    }
                }
            }

            return index;
        }

        /// <summary>
        /// Loads the tweet text data for each id in the corpus.
        /// </summary>
        /// <param name="service">TwitterService</param>
        /// <param name="corpus">List of CorpusDataRow</param>
        /// <param name="outputPath">File path to output data file.</param>
        /// <returns>List of CorpusDataRow (with Tweet DTO populated).</returns>
        private static List<CorpusDataRow> LoadTweets(TwitterService service, List<CorpusDataRow> corpus, string outputPath)
        {
            int skipCount = 0;
            int saveCount = 0;

            for (int index = GetResumeIndex(corpus, outputPath); index < corpus.Count; index++)
            {
                CorpusDataRow row = corpus[index];

                // Fetch the tweet.
                var status = service.GetTweet(new GetTweetOptions() { Id = row.Id });

                if (service.Response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // Convert the TwitterStatus to a Tweet DTO.
                    row.Tweet = Mapper.Map<TwitterStatus, Tweet>(status);

                    // Save the result to file.
                    SaveResult(row, outputPath);

                    saveCount++;

                    if ((index + 1) % 50 == 0)
                    {
                        Console.WriteLine("Saved " + (index + 1) + " tweets.");
                    }
                }
                else
                {
                    // Check the rate limit.
                    TwitterRateLimitStatus rateSearch = service.Response.RateLimitStatus;
                    if (rateSearch.RemainingHits < 1)
                    {
                        DateTime resetTime = rateSearch.ResetTime + TimeSpan.FromMinutes(1);

                        Console.WriteLine("Rate Limit reached. Sleeping until " + resetTime);
                        Thread.Sleep(resetTime - DateTime.Now);

                        // Try this record again.
                        index--;
                    }
                    else
                    {
                        // Some other error. Maybe 404. Skip this record.
                        skipCount++;
                        Console.WriteLine("Skipped " + skipCount + " records. Got " + service.Response.StatusCode + ".");
                    }
                }
            }

            Console.WriteLine("Saved " + saveCount + ", Skipped " + skipCount + ".");

            return corpus;
        }

        #endregion

        static void Main(string[] args)
        {
            // Setup AutoMapper.
            Mapper.CreateMap<TwitterStatus, Tweet>()
                .ForMember(dest => dest.ScreenName, opt => opt.MapFrom(source => source.Author.ScreenName));

            // Load tweet ids from the corpus.csv.
            List<CorpusDataRow> corpus = LoadCorpus(_corpusPath);

            Console.WriteLine("Twitter Sentiment Corpus Loader v1.0");
            Console.WriteLine("Loaded " + corpus.Count + " records.");
            Console.WriteLine();
            Console.WriteLine("Please login to Twitter and copy the auth code.");

            // Login to Twitter.
            TwitterService service = LoginTwitter();
           
            // Get tweets.
            corpus = LoadTweets(service, corpus, _outputPath);

            Console.WriteLine("Completed at " + DateTime.Now + ".");
            Console.ReadKey();
        }
    }
}
