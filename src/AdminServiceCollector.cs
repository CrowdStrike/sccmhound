using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SCCMHound.src.models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.DirectoryServices;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SCCMHound
{
    public class AdminServiceCollector
    {
        AdminServiceConnector connector;

        public AdminServiceCollector(AdminServiceConnector connector)
        {
            this.connector = connector;
        }

        public Boolean GetCollections()
        {
            string uri = "Collections('SMS00001')";

            HttpResponseMessage response = this.connector.GetAsync(uri);


            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStringAsync().Result;
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new UnauthorizedAccessException(response.ReasonPhrase);

            }
            return false;
        }

        public int SubmitCMPivotQuery(string query)
        {
            Console.WriteLine($"Executing {query} on All Systems Device Collection via pivot mechanism...");
            string uri = "Collections('SMS00001')/AdminService.RunCMPivot";
            string body = JsonConvert.SerializeObject(new { InputQuery = query });

            HttpResponseMessage response = this.connector.PostAsync(uri, new StringContent(body, Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var jsonObject = JObject.Parse(response.Content.ReadAsStringAsync().Result);

                return Int32.Parse((string)jsonObject["OperationId"]);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return -403;
            }
            return -1;

        }

        public JToken RetrieveCMPivotResult(int operationId, int sleepSeconds, int checkThreshold)
        {
            Console.WriteLine($"Retrieving results...");

            int loopCount = 0;
            string uri = $"SMS_CMPivotStatus?$filter=ClientOperationId eq {operationId}";
            JToken results = null;

            while (loopCount < checkThreshold)
            {
                HttpResponseMessage response = this.connector.GetAsync(uri);

                var result = response.Content.ReadAsStringAsync().Result;

                Debug.Print("Retrieving CMPivot results");
                if (response.IsSuccessStatusCode)
                {
                    JObject returned = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                    JToken tempResults = returned["value"];
                    if (results != null) {
                                            Console.WriteLine($"Retrieved {results.Count()} results");

                        if (results.Count() == tempResults.Count())
                        {
                            loopCount++;
                        }
                    }
                    else
                    {
                        loopCount++;
                    }
                    results = tempResults;
                    System.Threading.Thread.Sleep(sleepSeconds * 1000);
                }
            }

            return results;

            
            
        }

        public List<UserMachineRelationship> GetUsers()
        {
            var relationships = new List<UserMachineRelationship>();
            int job = SubmitCMPivotQuery("User");

            if (job < 0)
            {
                if (job == -403)
                {
                    throw new UnauthorizedAccessException("You do not have sufficient permissions to create CMPivot jobs");
                }
                else
                {
                    throw new Exception("An unknown error occured");
                }
            }

            JToken results = RetrieveCMPivotResult(job, 30, 3); //30 second sleep, 3 matches required to complete 
            foreach (JToken result in results)
            {
                string deviceName = (string)result["DeviceName"];
                string xmlOutput = (string)result["ScriptOutput"];

                try
                {
                    XDocument oScriptOutput = XDocument.Parse(xmlOutput);
                    List<UserMachineRelationship> tempUserMachineRelationships = oScriptOutput.Descendants("e").Select(
                        e => new UserMachineRelationship(
                            deviceName,
                            e.Attribute("UserName").Value)).ToList();

                    foreach (UserMachineRelationship relationship in tempUserMachineRelationships)
                    {
                        relationships.Add(relationship);
                    }
                }
                catch (Exception e)
                {
                    // couldnt parse
                }
                
            }

            return relationships;
        }

        public List<LocalAdmin> GetAdministrators()
        {
            var localAdmins = new List<LocalAdmin>();
            int job = SubmitCMPivotQuery("Administrators");

            if (job < 0)
            {
                if (job ==  -403)
                {
                    throw new UnauthorizedAccessException("You do not have sufficient permissions to create CMPivot jobs");
                }
                else
                {
                    throw new Exception("An unknown error occured");
                }
            }

            JToken results = RetrieveCMPivotResult(job, 30, 3);

            foreach (JToken result in results)
            {
                string deviceName = (string)result["DeviceName"];
                string xmlOutput = (string)result["ScriptOutput"];

                try
                {

                    XDocument oScriptOutput = XDocument.Parse(xmlOutput);
                    List<LocalAdmin> tempLocalAdmins = oScriptOutput.Descendants("e").Select(
                        e => new LocalAdmin(
                            e.Attribute("ObjectClass").Value,
                            e.Attribute("Name").Value,
                            deviceName)).ToList();

                    foreach (LocalAdmin localAdmin in tempLocalAdmins)
                    {
                        localAdmins.Add(localAdmin);
                    }
                }
                catch (Exception e)
                {
                    // couldnt parse
                }
            }

            

            return localAdmins;
        }
    }
}
