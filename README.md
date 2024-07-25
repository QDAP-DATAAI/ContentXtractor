# ContentXtractor

ContentXtractor is a tool used to extract content from web pages into Markdown format. It primarily uses Chrome's Reading Mode, if available on the page, and falls back to using the normal HTML of the page if Reading Mode is not available.

This project is based on Azure Function and has a [Docker image](Dockerfile) ready to be deployed. The Docker image contains [Chrome for Testing](https://googlechromelabs.github.io/chrome-for-testing/).

It uses [PuppeteerSharp](https://github.com/hardkoded/puppeteer-sharp) to interact with the [DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/) and [Turndown](https://github.com/mixmark-io/turndown) to convert HTML into Markdown.

## Table of Contents

- [Installation](#installation)
- [Usage](#usage)

## Installation

Follow these steps to install and set up the project:

```bash
# Clone the repository
git clone https://github.com/QDAP-DATAAI/ContentXtractor.git
cd ContentXtractor
```

### Download Chrome's dependencies:

Run `.\Extract\assets\download_turndown.ps1` to download [Turndown](https://github.com/mixmark-io/turndown).

Run `.\Extract\assets\download_screen_ai.ps1` to download [Chrome Screen AI Library](https://github.com/chromium/chromium/tree/main/chrome/browser/screen_ai). Note that it will download only the Linux version. Edit the file and change the var `$operatingSystem` to `windows` if you need.

```
# Run the project or open it in your IDE
func start
```

If everything is set up correctly, the URL will be printed in the console. Open this URL in your browser to use the application. It might look something like this:

http://localhost:7071/api/swagger/ui

## Usage

### Request

Here is how you can use ContentXtractor to extract content from web pages:

The most basic usage is to provide just the URL of the page that should be extracted. This is done by making a POST request, for example:

```json
{
  "url": "https://www.example.com/"
}
```

Example of a request using the full payload:
```json
{
  "url": "https://www.example.com/",
  "viewPortOptions": {
    "width": 1920,
    "height": 1080,
    "isMobile": false,
    "deviceScaleFactor": 1,
    "isLandscape": true,
    "hasTouch": false
  },
  "disableLinks": true,
  "returnRawHtml": true,
  "waitUntil": "Load",
  "readingModeTimeout": 5000
}
```

`url`: This is the url to be extracted. Only http and https are allowed.

`viewPortOptions`: An optional object to define the viewport settings for the extraction process. As this project uses PuppeteerSharp, this is the object provided by the library. More details [here](https://www.puppeteersharp.com/api/PuppeteerSharp.ViewPortOptions.html). Default or if null is an 800x600 viewport.

`disableLinks`: An optional parameter that, if set to true, will "click" the "Enable Links" button on the Reading Mode toolbar once, effectively disabling the links. Default is false. Note that this will have effect only if Reading Mode is available.

`returnRawHtml`: An optional parameter to return the raw HTML of the page in the response. Default is false.

`waitUntil`: An optional parameter to specify when the extraction should wait for a page to load. Options include Load, DOMContentLoaded, Networkidle0, and Networkidle2. This corresponds to PuppeteerSharp's [WaitUntilNavigation](https://www.puppeteersharp.com/api/PuppeteerSharp.WaitUntilNavigation.html) enum. Default or if null is `Load`.

`readingModeTimeout`: An optional parameter to define the timeout (in milliseconds) to wait for Chrome's Reading Mode to be ready before falling back to the raw HTML of the page. Default or if null is 15000 (15 seconds). Setting this to 0 will disable the timeout, which is not recommended.

### Response
If everything works, the response will be something like:

```json
{
  "success": true,
  "rawHtml": null,
  "markdown": "**Example Domain**\n==================\n\nThis domain is for use in illustrative examples in documents. You may use this domain in literature without prior coordination or asking for permission.",
  "extractedFromReadingMode": true,
  "urls": [
    "https://www.iana.org/domains/example"
  ],
  "requestResult": {
    "url": "https://www.example.com/",
    "contentType": "text/html; charset=UTF-8",
    "redirected": false,
    "statusCode": 200
  }
}
```

`success`: Indicates whether the content extraction was successful.

`rawHtml`: Contains the raw HTML of the web page if returnRawHtml was set to true. If returnRawHtml was false, this will be null.

`markdown`: The extracted content of the web page formatted in Markdown.

`extractedFromReadingMode`: Indicates whether the content was extracted using Chrome's Reading Mode. true means Reading Mode was used, and false means the extraction was done from the normal HTML of the page because Reading Mode wasn't available or timed out.

`urls`: An array of URLs found within the extracted content. These are the `<a>` tags found with `href` set to something.

`requestResult`: Contains detailed information about the original HTTP request to the URL.
`url`: The URL of the web page from which the content was extracted. This field returns the URL Chrome used for the request. For example, if the URL used in the payload was `http://www.Example.com/page#content`, this field will return `http://www.example.com/page`. In case of a redirect response, this field will show the URL Chrome was redirected to.
`contentType`: The content type returned by the server.
`redirected`: Indicates whether the request was redirected.
`statusCode`: The HTTP status code received from the server.


### Response Errors
Other errors can also be returned as Bad Request, for example:

```json
{
  "error": "net::ERR_NAME_NOT_RESOLVED at ..."
}
