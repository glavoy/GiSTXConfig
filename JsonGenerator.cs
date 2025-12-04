using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GistConfigX
{
    public class SurveyManifest
    {
        public string surveyName { get; set; }
        public string surveyId { get; set; }
        public string databaseName { get; set; }
        public List<string> xmlFiles { get; set; }
        public List<Crf> crfs { get; set; }
    }

    public class Crf
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? display_order { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string tablename { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string displayname { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? isbase { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string primarykey { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string linkingfield { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IdConfig idconfig { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string parenttable { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string incrementfield { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? requireslink { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string repeat_count_source { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string repeat_count_field { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? auto_start_repeat { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? repeat_enforce_count { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string display_fields { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string entry_condition { get; set; }
    }

    public class IdConfig
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string prefix { get; set; }
        [JsonConverter(typeof(SingleLineArrayConverter))]
        public List<IdConfigField> fields { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? incrementLength { get; set; }
    }

    public class IdConfigField
    {
        public string name { get; set; }
        public int length { get; set; }
    }

    public class JsonGenerator
    {
        public void Generate(string outputPath, SurveyManifest manifest)
        {
            string json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            File.WriteAllText(outputPath, json);
        }
    }

    class SingleLineArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(List<IdConfigField>));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteRawValue(JsonConvert.SerializeObject(value, Formatting.None));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize(reader, objectType);
        }

        public override bool CanRead
        {
            get { return true; }
        }
    }
}
