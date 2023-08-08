﻿using System.Globalization;
using System.Text;
using System.Xml.Serialization;
using TE.FileWatcher.Net;

namespace TE.FileWatcher.Configuration
{
    /// <summary>
    /// A notification that will be triggered.
    /// </summary>
    public class Notification : PlaceholderBase
    {
        // The message to send with the request.
        private readonly StringBuilder _message;

        // The full path of the changed file/folder
        private string? _fullPath;

        // The watch path
        private string? _watchPath;

        /// <summary>
        /// Gets or sets the URL of the request.
        /// </summary>
        [XmlElement("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the string representation of the request method.
        /// </summary>
        [XmlElement("method")]
        public string? MethodString { get; set; }

        /// <summary>
        /// Gets the request method.
        /// </summary>
        [XmlIgnore]
        public HttpMethod Method
        {
            get
            {
                if (string.IsNullOrWhiteSpace(MethodString))
                {
                    return HttpMethod.Post;
                }

                return MethodString.ToLower(CultureInfo.CurrentCulture) switch
                {
                    "get" => HttpMethod.Get,
                    "delete" => HttpMethod.Delete,
                    "put" => HttpMethod.Put,
                    _ => HttpMethod.Post,
                };
            }
        }

        /// <summary>
        /// Gets or sets the triggers of the request.
        /// </summary>
        [XmlElement("triggers")]
        public Triggers? Triggers { get; set; }

        /// <summary>
        /// Gets or sets the data to send for the request.
        /// </summary>
        [XmlElement("data")]
        public Data? Data { get; set; }

        /// <summary>
        /// Returns a value indicating if there is a message waiting to be sent
        /// for the notification.
        /// </summary>
        [XmlIgnore]
        public bool HasMessage
        {
            get
            {
                if (_message == null)
                {
                    return false;
                }

                return _message.Length > 0;
            }
        }

        /// <summary>
        /// Initializes an instance of the <see cref="Notification"/>class.
        /// </summary>
        public Notification()
        {
            _message = new StringBuilder();
        }

        /// <summary>
        /// Sends the notification.
        /// </summary>
        /// <param name="message">
        /// The value that replaces the <c>[message]</c> placeholder.
        /// </param>
        /// <param name="trigger">
        /// The trigger for the request.
        /// </param>
        /// <param name="watchPath">
        /// The watch path.
        /// </param>
        /// <param name="fullPath">
        /// The full path of the changed file/folder.
        /// </param>
        internal void QueueRequest(string message, TriggerType trigger, string watchPath, string fullPath)
        {
            if (Triggers == null || Triggers.TriggerList == null || Triggers.TriggerList.Count <= 0)
            {
                return;
            }

            if (Triggers.Current.HasFlag(trigger))
            {
                _message.Append(CleanMessage(message) + @"\n");
            }

            _fullPath = fullPath;
            _watchPath = watchPath;
        }

        /// <summary>
        /// Send the notification request.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the URL is null or empty.
        /// </exception>
        /// <exception cref="UriFormatException">
        /// Thrown when the URL is not in a valid format.
        /// </exception>
        internal async Task<Response?> SendAsync()
        {
            // If there isn't a message to be sent, then just return
            if (_message == null || _message.Length <= 0)
            {
                return null;
            }

            if (GetUri() == null)
            {
                throw new InvalidOperationException("The URL is null or empty.");
            }

            if (Data == null)
            {
                //throw new InvalidOperationException("Data for the request was not provided.");
                Data = new Data();
            }

            string content = string.Empty;
            if (Data.Body != null)
            {
                content = Data.Body.Replace("[message]", _message.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            Response response =
                await Request.SendAsync(
                    Method,
                    GetUri(),
                    Data.Headers,
                    content,
                    Data.MimeType).ConfigureAwait(false);

            _message.Clear();
            return response;
        }

        /// <summary>
        /// Escapes the special characters in the message so it can be sent as
        /// a JSON string.
        /// </summary>
        /// <param name="s">
        /// The message to escape.
        /// </param>
        /// <returns>
        /// The JSON string with the special characters escaped.
        /// </returns>
        private static string CleanMessage(string s)
        {
            if (s == null || s.Length == 0)
            {
                return "";
            }

            char c = '\0';
            int i;
            int len = s.Length;
            StringBuilder sb = new(len + 4);
            string t;

            for (i = 0; i < len; i += 1)
            {
                c = s[i];
                switch (c)
                {
                    case '\\':
                    case '"':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '/':
                        sb.Append('\\');
                        sb.Append(c);
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        if (c < ' ')
                        {
                            t = "000" + string.Format(CultureInfo.CurrentCulture,"{0:X}", c);
                            sb.Append(string.Concat("\\u", t.AsSpan(t.Length - 4)));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the URI value of the string URL.
        /// </summary>
        /// <exception cref="UriFormatException">
        /// Thrown if the URL is not in a valid format.
        /// </exception>
        private Uri GetUri()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                throw new UriFormatException();
            }

            string? url = ReplacePlaceholders(Url, _watchPath, _fullPath);
            if (!string.IsNullOrWhiteSpace(url))
            {
                url = ReplaceFormatPlaceholders(url, _watchPath, _fullPath);
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new UriFormatException($"The notification URL: {url} is not valid.");
            }

            Uri uri = new(url);
            return uri;
        }
    }
}
