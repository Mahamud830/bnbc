using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace BnbcUltimatePro
{
    [DataContract]
    public class PatternSpec
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Type { get; set; }
        [DataMember] public double SelfWeight { get; set; }
        [IgnoreDataMember] public string Status { get; set; }
    }

    [DataContract]
    public class ProjectInfo
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Town { get; set; }
        [DataMember] public string Soil { get; set; }
        [DataMember] public string Occupancy { get; set; }
        [DataMember] public string Stories { get; set; }
        [DataMember] public string Notes { get; set; }
        [DataMember] public List<PatternSpec> Patterns { get; set; }

        public static void Save(string path, ProjectInfo p)
        {
            var ser = new DataContractJsonSerializer(typeof(ProjectInfo));
            using (var fs = File.Create(path)) ser.WriteObject(fs, p);
        }

        public static ProjectInfo Load(string path)
        {
            var ser = new DataContractJsonSerializer(typeof(ProjectInfo));
            using (var fs = File.OpenRead(path)) return (ProjectInfo)ser.ReadObject(fs);
        }
    }
}
