# PA4

Azure Website: http://pa4cloud.cloudapp.net/

#### End-to-End Experience:

The webcarwler crawls CNN and BleacherReport and gathers urls and inserts them into a queue. From the queue, the urls are pulled and parsed through, recording each word in the title, as well as the date and url. Each keyword is put in the table separately along with its title and date.

Searching through the index.html allows the user to see results that include the keyword in the title of the article, ranked by number of keywords, then by date. Duplicate titles are removed. This is all done through a LINQ query and returned as a JSON string, when the AJAX request from the index.html is passed.

When an NBA player is searched, a cross domain AJAX request is used to access a PHP file(index.php), which queries an AWS RDS containing statistics for NBA players. The information creates a Player object, whos relevant stats are returned to the index.html. Both the database and php files are hosted on AWS.

The query suggestion service is created by downloading a file from Azure blob storage, containing titles from wikipedia for names A-C. A Trie is built from these titles and are returned to the user via an AJAX request to a webmethod that searched the trie. The Trie is kept in memory using the service StillAlive, which runs the the search function every minute.

The dashboard shows relevant crawling information, including CPU usage, RAM usage, crawling status, number of urls crawled, number of urls in the queue, number of urls in the index, number of titles in the trie, and the last title in the trie. All of this information is stored in separate tables according to the processes involved. The dashboard also allows for searching with a url, and recieving a title, url, and date for the url given. The last 10 urls crawled, and the last 10 errors recieved are shown on the dashboard. If a user tries to start crawling while the crawler is Loading, or Crawling, nothing will happen, as to prevent duplicate robot.txs to be processed.

Caching is done using a dictionary that is created when the first search is ennacted. If the dictionary has more than 100 elements, any elements that are older than 20 minutes will be removed.

Monetization is achieved by implementing a script at the top of the body, provided via Google Ads.

#### Code Written in C# - C# best practices:
All table entities are stored in their own class. There are separate objects for each class that are stored in a ClassLibrary, so they may be used throguhout the project.

There are two html files, index.html for the user front end, and dashboard.html for the user backend.

The Admin.asmx file handles all code that is shown in the dashboard, while the Search.asmx handles all code that is shown on the front page.

#### Works on Azure & AWS
The PHP files, and a MySQL database are stored on AWS.

The website is running on Windows Azure (url above).

There are two webroles, Admin.asmx and Search.asmx, each used to control different html pages and show the appropriate information to each user.

When the program starts, robot.txt files are input through a command queue, parse through, and XML files are input into an XML queue. All XML files are fully crawled and initial HTML urls are input into an HTML queue in the loading phase, before any HTML parsing and table storage occurs.

There is a single worker role running that is crawling through CNN and BleacherReport with urls taken from a single URL queue. The urls are parsed using HTMLAgilityPack and stored in an HTML table.

Performance information, as well as errors, and trie information are stored in tables on Azure for the Admin.asmx to query.