TwitterCorpus

This is a tweet loader for a Twitter corpus, used for sentiment prediction. The corpus contains 5,513 tweets with sentiment rating.

Due to Twitter TOS, only the tweet ids may be provided in the corpus. Thus, it is required to manually collect each tweet's details, based on the id. This program does just that.

As the program uses OAuth to login to Twitter, you'll need to set the consumerKey and consumerSecret in the App.Config before starting.

Example data row
"apple","positive","123456789012345678"

Example output row
123456789012345678,1/1/2010 9:01:30 PM,en,someuser,@Apple just released a new version of the iPhone. Cool!