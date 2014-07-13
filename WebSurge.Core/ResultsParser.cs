﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Westwind.RazorHosting;
using Westwind.Utilities;

namespace WebSurge
{
    public class ResultsParser
    {

        public TestResult ParseResults(IEnumerable<HttpRequestData> resultData, int totalTimeSecs, int threads)
        {
            // avoid divide by zero errors
            if (totalTimeSecs < 1)
                totalTimeSecs = 1;
            
            var results = resultData.ToList();

            var res = new TestResult()
            {
                TotalRequests = results.Count,
                ThreadCount = threads,
                TimeTakenSecs = totalTimeSecs,
                FailedRequests = results.Count(req => req.IsError),
                RequestsPerSecond = ((decimal) results.Count/(decimal) totalTimeSecs),
                AvgRequestTimeMs = (decimal) results.Average(req => req.TimeTakenMs),
                MinRequestTimeMs = results.Min(req => req.TimeTakenMs),
                MaxRequestTimeMs = results.Max(req => req.TimeTakenMs)
            };

            return res;
        }

        public string ParseResultsToString(IEnumerable<HttpRequestData> resultData, int totalTimeSecs, int threads)
        {

            var result = ParseResults(resultData, totalTimeSecs, threads);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Total Requests: " + result.TotalRequests.ToString("n0"));

            if (threads > 0)
                sb.AppendLine("       Threads: " + result.ThreadCount);

            sb.AppendLine("        Failed: " + result.FailedRequests);

            if (result.TimeTakenSecs > 0)
            {
                sb.AppendLine("    Total Time: " + result.TimeTakenSecs.ToString("n2") + " secs");
                if (result.TotalRequests > 0)
                    sb.AppendLine("       Req/Sec: " +
                                  ((decimal) result.TotalRequests/(decimal) result.TimeTakenSecs).ToString("n2") +
                                  "\r\n");
            }
            if (result.TotalRequests > 0)
            {
                sb.AppendLine(string.Format("      Avg Time: {0:n2} ms", result.AvgRequestTimeMs));
                sb.AppendLine(string.Format("      Min Time: {0:n2} ms", result.MinRequestTimeMs));
                sb.AppendLine(string.Format("      Max Time: {0:n2} ms", result.MaxRequestTimeMs));
            }

            return sb.ToString();
        }

        public IEnumerable<TimeTakenResult> TimeLineDataForIndividualRequest(IEnumerable<HttpRequestData> resultData,
            string url)
        {
            var count = 0;
            return resultData.Where(rd => rd.Url.ToLower() == url.ToLower())
                .OrderBy(rd => rd.Timestamp)
                .Select(rd => new TimeTakenResult()
                {
                    OrigId = rd.Id,
                    RequestNo = count++,
                    TimeTaken = rd.TimeTakenMs,
                    IsError = rd.IsError
                }).ToList();
        }

        public IEnumerable<RequestsPerSecondResult> RequestsPerSecond(IEnumerable<HttpRequestData> resultData,
            string url = null)
        {

            if (!string.IsNullOrEmpty(url))
                resultData = resultData.Where(rd => rd.Url.ToLower() == url.ToLower());


            DateTime startTime = resultData.First().Timestamp;
            var res = resultData.OrderBy(rd => rd.Timestamp)
                .GroupBy(rd => (int) rd.Timestamp.Subtract(startTime).TotalSeconds,
                    rd => rd,
                    (second, rd) => new RequestsPerSecondResult() {Second = second, Requests = rd.Count()});

            return res.ToList();
        }

        public IEnumerable<UrlSummary> UrlSummary(IEnumerable<HttpRequestData> resultData, int totalTimeTakenSecs)
        {
            // avoid divide by 0 error - assume at least 1 second
            if (totalTimeTakenSecs == 0)
                totalTimeTakenSecs = 1;

            var urls = resultData
                .GroupBy(res => res.HttpVerb +  " " + res.Url, rs => rs, (key, uls) =>
                    new UrlSummary()
                    {
                        Url = key,                        
                        Results = new TestResult()
                        {
                            TimeTakenSecs = totalTimeTakenSecs,
                            TotalRequests = uls.Count(),
                            FailedRequests = uls.Count(u => u.IsError),
                            SuccessRequests = uls.Count(u => !u.IsError),
                            RequestsPerSecond = ((decimal) uls.Count()/(decimal) totalTimeTakenSecs),
                            MinRequestTimeMs   = uls.Min( u=> u.TimeTakenMs),
                            MaxRequestTimeMs = uls.Max(u => u.TimeTakenMs),
                            AvgRequestTimeMs = (decimal) uls.Average(u=> u.TimeTakenMs),                            
                        }
                    });



            return urls.ToList();
        }

        
        public string ResultReportHtml(IEnumerable<HttpRequestData> resultData, int totalTimeTaken, int threadCount)
        {
            var urlSummary = UrlSummary(resultData, totalTimeTaken);
            var testResult = ParseResults(resultData, totalTimeTaken, threadCount);

            var model = new TestResultView()
            {
                TestResult = testResult,
                UrlSummary = urlSummary
            };
            return RenderTemplate("TestResult.cshtml",model);
        }


        private Dictionary<string, string> compiledTemplates = new Dictionary<string, string>();
        private RazorEngine<RazorTemplateBase> host = CreateHost();

        private static RazorEngine<RazorTemplateBase> CreateHost()
        {
            var host = new RazorEngine<RazorTemplateBase>();

            // add this assembly
            host.AddAssemblyFromType(typeof(ResultsParser));            
            
            return host;
        }

        private string RenderTemplate(string templateName, object model)
        {            
            string compiledId = null;
            if (compiledTemplates.Keys.Contains(templateName))
                compiledId = compiledTemplates[templateName];
            else
            {
                string template = File.ReadAllText(App.UserDataPath + "Templates\\" + templateName);                
                compiledId = host.CompileTemplate(template);

                if (compiledId == null)
                    return "<pre>" + host.ErrorMessage + "\r\n------\r\n" + host.LastGeneratedCode + "</pre>";                    
                
                compiledTemplates.Add(template, compiledId);
            }

            string result = host.RenderTemplateFromAssembly(compiledId,model);

            if (result == null)
                result = "<pre>" + host.ErrorMessage + "\r\n------\r\n" + host.LastGeneratedCode + "</pre>";

            return result;
        }
    }

    public class TestResultView
    {
        public TestResult TestResult { get; set; }
        public IEnumerable<UrlSummary> UrlSummary { get; set; }    
    }

    public class UrlSummary
    {
        public string Url { get; set; }
        public string HttpVerb { get; set;  }
        public TestResult Results { get; set; }
    }

    public class TestResult
    {
        public int TotalRequests { get; set; }
        public int ThreadCount { get; set; }
        public int FailedRequests { get; set; }
        public int SuccessRequests { get; set; }
        public decimal RequestsPerSecond { get; set; }        
        public decimal AvgRequestTimeMs { get; set; }
        public decimal MinRequestTimeMs { get; set; }
        public decimal MaxRequestTimeMs { get; set; }
        public int TimeTakenSecs { get; set; }
    }

    public class RequestsPerSecondResult
    {
        public int Second { get; set; }
        public int Requests { get; set; }
    }

    public class TimeTakenResult
    {
        public long OrigId { get; set; }
        public int RequestNo { get; set; }
        public int TimeTaken { get; set; }
        public bool IsError { get; set; }
    }

}