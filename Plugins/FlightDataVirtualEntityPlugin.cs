using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SF365.Plugins
{
    public class FlightDataVirtualEntityPlugin : PluginBase
    {
        public FlightDataVirtualEntityPlugin() : base(typeof(FlightDataVirtualEntityPlugin))
        {

        }
        private ITracingService _tracer;
        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            _tracer = localcontext.TracingService;
            switch (localcontext.PluginExecutionContext.MessageName)
            {
                case "RetrieveMultiple":
                    _tracer.Trace("RetrieveMultiple");
                    RetrieveMultiple(localcontext);
                    break;
                case "Retrieve":
                    _tracer.Trace("Retrieve");
                    Retrieve(localcontext);
                    break;
            }

        }

        private void Retrieve(LocalPluginContext localcontext)
        {
            var target = (EntityReference)localcontext.PluginExecutionContext.InputParameters["Target"];
            var record = GetFlightData(target.Id, null).FirstOrDefault();
            localcontext.PluginExecutionContext.OutputParameters["BusinessEntity"] = record;
        }

        private void RetrieveMultiple(LocalPluginContext localcontext)
        {
            try
            {
                _tracer.Trace("RetrieveMultiple_1");
                var query = (QueryBase)localcontext.PluginExecutionContext.InputParameters["Query"];
                _tracer.Trace("RetrieveMultiple_2");
                if (query == null)
                    return;

                // Get the flight number query
                _tracer.Trace("RetrieveMultiple_3");
                var expression = query as QueryExpression;
                _tracer.Trace("RetrieveMultiple_4");
                var filter = expression.Criteria.Conditions.Where(c => c.AttributeName == "sf365_name").FirstOrDefault();
                _tracer.Trace("RetrieveMultiple_5");
                var flightnumber = filter?.Values[0].ToString();
                _tracer.Trace("RetrieveMultiple_6");
                var data = GetFlightData(null, flightnumber);

                localcontext.PluginExecutionContext.OutputParameters["BusinessEntityCollection"] =
                    new EntityCollection(data);
            }
            catch (InvalidPluginExecutionException ex)
            {

                throw new InvalidPluginExecutionException(ex.Message);
            }

        }

        private List<Entity> GetFlightData(Guid? flightid, string flightnumber)
        {

            _tracer.Trace("RetrieveMultiple_GetFlightData_1");
            var random = new Random();
            _tracer.Trace("RetrieveMultiple_GetFlightData_2");
            var position = GetLoacationAsync().Result;

            var data = new List<Entity>
                {

                    new Entity("sf365_flightdata")
                    {
                        ["sf365_flightdataid"] = new Guid("00000000-0000-0000-0000-000000000111"),
                        ["sf365_name"] = "111",
                        ["sf365_latitude"] = ((Position)position).Lat,
                        ["sf365_longitude"] = position.Lon
                    },
                    new Entity("sf365_flightdata")
                    {
                        ["sf365_flightdataid"] = new Guid("00000000-0000-0000-0000-000000000112"),
                        ["sf365_name"] = "112",
                        ["sf365_latitude"] = (Decimal) random.NextDouble(),
                        ["sf365_longitude"] = (Decimal) random.NextDouble()
                    }
            };

            if (flightid != null)
            {
                return data
                    .Where(d => d.GetAttributeValue<Guid>("sf365_flightdataid") == flightid)
                    .ToList();
            }
            else if (!string.IsNullOrEmpty(flightnumber))
            {
                return data
                    .Where(d => d.GetAttributeValue<string>("sf365_name")
                        .EndsWith(flightnumber))
                    .ToList();
            }

            return data;


        }

        public async Task<Position> GetLoacationAsync()
        {
            _tracer.Trace("RetrieveMultiple_GetLoacationAsync_1");
            Position result = null;
            _tracer.Trace("RetrieveMultiple_GetLoacationAsync_2");
            using (HttpClient client = new HttpClient(/*handler:httpClientHandler, disposeHandler: true*/))
            {

                //var requestUri = new Uri("https://api.fixer.io/latest?symbols=" + String.Join(",", currencyISOCodes));
                _tracer.Trace("RetrieveMultiple_GetLoacationAsync_3");
                var requestUri = new Uri("https://www.flightstats.com/v2/api/flick/968155817?guid=34b64945a69b9cac:5ae30721:13ca699d305:XXXX&airline=CX&flight=829&flightPlan=true&rqid=sz2ouhyipv");

                _tracer.Trace("RetrieveMultiple_GetLoacationAsync_3.1");
                //var requestUri = new Uri("http://free.currencyconverterapi.com/api/v5/convert?q=" + String.Join("_", currencyISOCodes)+ "&compact=y");
                _tracer.Trace("RetrieveMultiple_GetLoacationAsync_4");
                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                _tracer.Trace("RetrieveMultiple_GetLoacationAsync_5");
                var response = await client.SendAsync(request);

                _tracer.Trace("RetrieveMultiple_GetLoacationAsync_6");
                if (!response.IsSuccessStatusCode)
                    throw new InvalidPluginExecutionException("Exchangerate service returned " + response);

                _tracer.Trace("RetrieveMultiple_GetLoacationAsync_7");

                var json = response.Content.ReadAsStringAsync().Result;


                //var setting = new DataContractJsonSerializerSettings()
                //{
                //    UseSimpleDictionaryFormat = true
                //};
                _tracer.Trace(json.ToString());

                var positions = JObject.Parse(json)["positions"];

                _tracer.Trace(positions.ToString());

                //throw new InvalidPluginExecutionException(positions.ToString());



                //result = (JsonConvert.DeserializeObject<Positions>(positions.ToString())).Position.FirstOrDefault();

                result = (positions.ToObject<Position[]>()).FirstOrDefault();
                _tracer.Trace(result.ToString());

                //using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(json)))
                //{
                //    var serializer = new DataContractJsonSerializer(typeof(Positions), setting);
                //    result = ((Positions)serializer.ReadObject(ms)).Position.FirstOrDefault();
                //}
            }

            return result;
        }


        public class Positions
        {
            public List<Position> Position;
        }


        public class Position
        {
            [JsonProperty("lon")]
            public decimal Lon { get; set; }
            [JsonProperty("lat")]
            public decimal Lat { get; set; }
            [JsonProperty("speedMph")]
            public int SpeedMph { get; set; }
            [JsonProperty("altitudeFt")]
            public int AltitudeFt { get; set; }

            [JsonProperty("source")]
            public string Source { get; set; }
            [JsonProperty("date")]
            public DateTimeOffset Date { get; set; }

        }
    }
}
