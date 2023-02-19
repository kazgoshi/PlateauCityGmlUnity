using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using System.Numerics;
using UnityEngine;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
#if UNITY_EDITOR
using Unity.EditorCoroutines.Editor;

using System.Collections;
namespace PlateauCityGml
{
    public enum CityObjectType { Undefined, Building, Relief };
    public class CityGMLParser
    {
        enum State { None, Name, 建物ID, SurfaceMember };

        public Position LowerCorner { get; set; } =  new Position { Latitude = -100, Longitude = -200};
        public Position UpperCorner { get; set; } = new Position { Latitude = 100, Longitude = 200 };

        public CityGMLParser()
        {

        }

        public CityGMLParser(Position lower, Position upper)
        {
            LowerCorner = lower;
            UpperCorner = upper;
        }
        public void setPositions(Position lower, Position upper)
        {
            LowerCorner = lower;
            UpperCorner = upper;
        }

        public CityObjectType GetCityObjectType(string gmlPath)
        {
            string fullPath = Path.GetFullPath(gmlPath);
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            const string bldgBuilding = "bldg:Building";
            const string demReliefFeature = "dem:ReliefFeature";

            CityObjectType coType = CityObjectType.Undefined;

            using (var fileStream = File.OpenText(gmlPath))
            using (XmlReader reader = XmlReader.Create(fileStream, settings))
            {
                while (reader.Read() && coType == CityObjectType.Undefined)
                {
                    //Debug.Log("GetCityObjectType " + reader.Name);/////////////
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == bldgBuilding)
                            {
                                coType = CityObjectType.Building;
                            }
                            else if(reader.Name == demReliefFeature)
                            {
                                coType = CityObjectType.Relief;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            return coType;
        }

        public Relief GetRelief(string gmlPath)
        {
            const string trianglePatches = "gml:trianglePatches";
            string fullPath = Path.GetFullPath(gmlPath);

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            Relief relief = null;

            using (var fileStream = File.OpenText(gmlPath))
            using (XmlReader reader = XmlReader.Create(fileStream, settings))
            {
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name == trianglePatches)
                            {
                                try
                                {
                                    relief = CreateRelief(reader);
                                    if(relief != null)
                                    {
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message); // Parse error
                                }
                            }

                            break;
                        default:
                            break;
                    }
                }

            }
            return relief;
        }

        public IEnumerator GetBuildingsLOD3(string gmlPath, List<Building> buildings, CorData cordata)
        {
            const string bldgBuilding = "bldg:Building";
            string fullPath = Path.GetFullPath(gmlPath);
            //List<Building> buildings = new List<Building>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            TextureInfo textures = null;

            using (var fileStream = File.OpenText(gmlPath))
            using (XmlReader reader = XmlReader.Create(fileStream, settings))
            {
                int iyd = 0;
                int count = 0;
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start(); //計測開始
                var sw2 = new System.Diagnostics.Stopwatch();
                sw2.Start(); //計測開始                
                Building building = null;
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            Debug.Log("GetBuildingsLOD3 "+reader.Name);////////
                            if (reader.Name == bldgBuilding)
                            {
                                try
                                {
                                    count++;
                                    building = CreateBuildingLOD3(reader);
                                    if(building.LOD1Solid != null || building.LOD2Solid != null)
                                    {
                                        building.GmlPath = fullPath;
                                        buildings.Add(building);
                                    }
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine(ex.Message); // Parse error
                                }
                            }
 
                            if (reader.Name == "app:appearanceMember")
                            {
                                //textures = ParseTextureInfo(reader, buildings);
                                var map = new Dictionary<string, (int index, Vector2[] uv)>();
                                List<string> textureFiles = new List<string>();

                                XmlDocument doc = new XmlDocument();
                                var r2 = reader.ReadSubtree();
                                XmlNode cd = doc.ReadNode(r2);
                                XmlNodeList list = cd.FirstChild.ChildNodes;
                                int tcount = list.Count;
                                //Debug.Log("ParseTextureInfo "+tcount);
                                for(int i = 0;i < tcount; i++) {
                                    if (i%100==0) {
                                        Debug.Log("ParseTextureInfo "+i+"/"+tcount);
                                    }
                                    if (list[i].Name == "app:surfaceDataMember") // 1枚のテクスチャに紐づくUV
                                    {
                                        if (list[i].FirstChild.Name == "app:ParameterizedTexture"){
                    //                        Debug.Log(i+" "+list[i].Name);
                                            XmlNodeList uv = list[i].FirstChild.ChildNodes;
                                            for(int j=0; j<uv.Count; j++)
                                            {
                                                if (uv[j].Name == "app:target")
                                                {
                                                    // UVデータを登録
                                                    string uri = uv[j]?.Attributes["uri"]?.Value;
                                                    if (uri==null) {
                                                        break;//return null;
                                                    }
                                                    uri = uri.Substring(1);
                                                    string texString = uv[j].FirstChild.FirstChild.FirstChild.Value;
                                                    map.Add(uri,(textureFiles.Count-1, ConvertToUV(texString)));
                                                    continue;
                                                }

                                                if (uv[j].Name == "app:imageURI")
                                                {
                                                    string file = uv[j].FirstChild.Value;
                                                    textureFiles.Add(file);
                                                    //Debug.Log("URI "+file);
                                                    continue;
                                                }
                                            }
                                        }

                                    }
                                    float elapsed2 = (float)sw2.Elapsed.TotalSeconds;   
                                    if (elapsed2>0.3) {
                                        sw2.Stop(); //計測終了
                                        sw2.Restart();
                                        sw2.Start();
                                        yield return null;  
                                    }                                      
                                }
                                //Debug.Log("return");
                    //            return null;
                                textures =  new TextureInfo { Files = textureFiles, Map = map };



                            }
                            break;
                        default:
                            break;
                    }
                    iyd++;          
                    //Debug.Log("Time.time "+ Time.time+" sw.Elapsed "+ sw.Elapsed+" sw2.Elapsed "+ sw2.Elapsed); //経過時間);        
                   float elapsed = (float)sw2.Elapsed.TotalSeconds;   
                    if (elapsed>0.3) {
                        sw2.Stop(); //計測終了
                        sw2.Restart();
                        sw2.Start();
                        yield return null;  
                    }                                    

                    //if (count==10) break;
                }
                sw.Stop(); //計測終了
                sw2.Stop(); //計測終了
            }
            if(textures != null && textures.Files.Count != 0)
            {
                // UVデータをビルデータにマージ
                MargeData(buildings, textures);

                RemoveNoTextureData(buildings);

            }
            cordata.cor1finished = true;
            // return buildings.ToArray();
        }
        public IEnumerator GetBuildings(string gmlPath, List<Building> buildings, CorData cordata)
        {
            const string bldgBuilding = "bldg:Building";
            string fullPath = Path.GetFullPath(gmlPath);
            //List<Building> buildings = new List<Building>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            TextureInfo textures = null;

            using (var fileStream = File.OpenText(gmlPath))
            using (XmlReader reader = XmlReader.Create(fileStream, settings))
            {
                int iyd = 0;
                int count = 0;
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start(); //計測開始
                var sw2 = new System.Diagnostics.Stopwatch();
                sw2.Start(); //計測開始                
                Building building = null;
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            Debug.Log("GetBuildingsLOD2 "+reader.Name);////////
                            if (reader.Name == bldgBuilding)
                            {
                                try
                                {
                                    count++;
                                    building = CreateBuilding(reader);
                                    if(building.LOD1Solid != null || building.LOD2Solid != null)
                                    {
                                        building.GmlPath = fullPath;
                                        buildings.Add(building);
                                    }
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine(ex.Message); // Parse error
                                }
                            }

                            if (reader.Name == "app:appearanceMember")
                            {
                                //textures = ParseTextureInfo(reader, buildings);
                                var map = new Dictionary<string, (int index, Vector2[] uv)>();
                                List<string> textureFiles = new List<string>();

                                XmlDocument doc = new XmlDocument();
                                var r2 = reader.ReadSubtree();
                                XmlNode cd = doc.ReadNode(r2);
                                XmlNodeList list = cd.FirstChild.ChildNodes;
                                int tcount = list.Count;
                                //Debug.Log("ParseTextureInfo "+tcount);
                                for(int i = 0;i < tcount; i++) {
                                    if (i%100==0) {
                                        Debug.Log("ParseTextureInfo "+i+"/"+tcount);
                                    }
                                    if (list[i].Name == "app:surfaceDataMember") // 1枚のテクスチャに紐づくUV
                                    {
                                        if (list[i].FirstChild.Name == "app:ParameterizedTexture"){
                    //                        Debug.Log(i+" "+list[i].Name);
                                            XmlNodeList uv = list[i].FirstChild.ChildNodes;
                                            for(int j=0; j<uv.Count; j++)
                                            {
                                                if (uv[j].Name == "app:target")
                                                {
                                                    // UVデータを登録
                                                    string uri = uv[j]?.Attributes["uri"]?.Value;
                                                    if (uri==null) {
                                                        break;//return null;
                                                    }
                                                    uri = uri.Substring(1);
                                                    string texString = uv[j].FirstChild.FirstChild.FirstChild.Value;
                                                    map.Add(uri,(textureFiles.Count-1, ConvertToUV(texString)));
                                                    continue;
                                                }

                                                if (uv[j].Name == "app:imageURI")
                                                {
                                                    string file = uv[j].FirstChild.Value;
                                                    textureFiles.Add(file);
                                                    //Debug.Log("URI "+file);
                                                    continue;
                                                }
                                            }
                                        }

                                    }
                                    float elapsed2 = (float)sw2.Elapsed.TotalSeconds;   
                                    if (elapsed2>0.3) {
                                        sw2.Stop(); //計測終了
                                        sw2.Restart();
                                        sw2.Start();
                                        yield return null;  
                                    }                                      
                                }
                                //Debug.Log("return");
                    //            return null;
                                textures =  new TextureInfo { Files = textureFiles, Map = map };



                            }
                            break;
                        default:
                            break;
                    }
                    iyd++;          
                    //Debug.Log("Time.time "+ Time.time+" sw.Elapsed "+ sw.Elapsed+" sw2.Elapsed "+ sw2.Elapsed); //経過時間);        
                   float elapsed = (float)sw2.Elapsed.TotalSeconds;   
                    if (elapsed>0.3) {
                        sw2.Stop(); //計測終了
                        sw2.Restart();
                        sw2.Start();
                        yield return null;  
                    }                                    

                    //if (count==10) break;
                }
                sw.Stop(); //計測終了
                sw2.Stop(); //計測終了
            }
            if(textures != null && textures.Files.Count != 0)
            {
                Debug.Log("GetBuildingsLOD2 MargeData");////////
                

                // UVデータをビルデータにマージ
                MargeData(buildings, textures);
            }
            Debug.Log("GetBuildingsLOD2 Return");////////
            cordata.cor1finished = true;
            // return buildings.ToArray();
        }

        public IEnumerator GetFRNs(string gmlPath, List<Building> buildings, CorData cordata,bool frnSplit)
        {
            //const string bldgBuilding = "bldg:Building";
            string fullPath = Path.GetFullPath(gmlPath);
            //List<Building> buildings = new List<Building>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            TextureInfo textures = null;
            int iyd = 0;
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start(); //計測開始
            var sw2 = new System.Diagnostics.Stopwatch();
            sw2.Start(); //計測開始  
            using (var fileStream = File.OpenText(gmlPath))
            using (XmlReader reader = XmlReader.Create(fileStream, settings))
            {
 


                //int count = 0;
                //Building building = null;
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            Debug.Log("GetFRNs "+reader.Name);////////
                            if (reader.Name == "frn:CityFurniture")
                            {
                                try{
                                    List<Building> newbuildings = CreateFRN(reader,fullPath,frnSplit);
                                //    List<Building> newbuildings = CreateFRNMulti(reader,fullPath);

                                    if (newbuildings != null) buildings.AddRange(newbuildings);
                                    // if(building.LOD1Solid != null || building.LOD2Solid != null)
                                    // {
                                    //     building.GmlPath = fullPath;
                                    //     buildings.Add(building);
                                    // }
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine(ex.Message); // Parse error
                                }
                            }

                            if (reader.Name == "app:appearanceMember")
                            {
                                textures = ParseTextureInfo(reader, buildings);
                            }
                            break;
                        default:
                            break;
                    }
                    iyd++;                    
                    if (iyd%2==0) {
                        yield return null;  
                    }
                    //Debug.Log("Time.time "+ Time.time+" sw.Elapsed "+ sw.Elapsed+" sw2.Elapsed "+ sw2.Elapsed); //経過時間);        
                   	float elapsed = (float)sw2.Elapsed.TotalSeconds;   
                    if (elapsed>0.3) {
                        sw2.Stop(); //計測終了
                        sw2.Restart();
                        sw2.Start();
                        yield return null;  
                    }  
                }
                
            }
            if(textures != null)
            {
                // UVデータをビルデータにマージ
                MargeData(buildings, textures);
            }
            sw.Stop(); //計測終了
            sw2.Stop(); //計測終了
            cordata.cor1finished = true;
            //return buildings.ToArray();
        }

    

        public IEnumerator GetVEGs(string gmlPath, List<Building> buildings, CorData cordata)
        {
            //const string bldgBuilding = "bldg:Building";
            string fullPath = Path.GetFullPath(gmlPath);
            //List<Building> buildings = new List<Building>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            TextureInfo textures = null;
            Dictionary<string, Color> matDic = null;
            int iyd = 0;
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start(); //計測開始
            var sw2 = new System.Diagnostics.Stopwatch();
            sw2.Start(); //計測開始      
            using (var fileStream = File.OpenText(gmlPath))
            using (XmlReader reader = XmlReader.Create(fileStream, settings))
            {
          
                //Building building = null;
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            Debug.Log("GetVEGs "+reader.Name);////////
                            if (reader.Name == "veg:PlantCover" || reader.Name == "veg:SolitaryVegetationObject")
                            {
                                // SolitaryVegetationObject は、 lod3MultiSurface 葉と幹？
                                try
                                {
                                    List<Building> newbuildings  = CreateVEGMulti(reader,fullPath);
                                    if (newbuildings != null) buildings.AddRange(newbuildings);
                                    //building = CreateVEG(reader);
                                    // if(building.LOD1Solid != null || building.LOD2Solid != null)
                                    // {
                                    //     building.GmlPath = fullPath;
                                    //     buildings.Add(building);
                                    // }
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine(ex.Message); // Parse error
                                }
                            }

                            if (reader.Name == "app:appearanceMember")
                            {
                                //textures = ParseTextureInfo(reader, buildings);
                                matDic = ParseMaterialInfoVEG(reader);
                                //Debug.Log(matDic.)
                            }
                            break;
                        default:
                            break;
                    }
                    iyd++;                    
                    if (iyd%100==0) {
                        yield return null;  
                    }    
                    //Debug.Log("Time.time "+ Time.time+" sw.Elapsed "+ sw.Elapsed+" sw2.Elapsed "+ sw2.Elapsed); //経過時間);        
                   			float elapsed = (float)sw2.Elapsed.TotalSeconds;   
                    if (elapsed>0.3) {
                        sw2.Stop(); //計測終了
                        sw2.Restart();
                        sw2.Start();
                        yield return null;  
                    }                                      
                }
                
            }
            if(textures != null)
            {
                // UVデータをビルデータにマージ
                MargeData(buildings, textures);
            }
            if (matDic != null) {
                MargeDataMat(buildings, matDic);
            }
            cordata.cor1finished = true;
                sw.Stop(); //計測終了
                sw2.Stop(); //計測終了            
            // eturn buildings.ToArray();
        }


        public IEnumerator GetTRANLOD3s(string gmlPath, List<Building> buildings, CorData cordata)
        {
            //const string bldgBuilding = "bldg:Building";
            string fullPath = Path.GetFullPath(gmlPath);
            //List<Building> buildings = new List<Building>();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            TextureInfo textures = null;
            Dictionary<string, Color> matDic = null;            
                int iyd = 0;
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start(); //計測開始
                var sw2 = new System.Diagnostics.Stopwatch();
                sw2.Start(); //計測開始     
            using (var fileStream = File.OpenText(gmlPath))
            using (XmlReader reader = XmlReader.Create(fileStream, settings))
            {
              
                //Building building = null;
                while (reader.Read())
                {

                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            //Debug.Log("GetTRANLOD3s "+reader.Name);////////
                            if (reader.Name == "tran:Road")
                            {
                                // SolitaryVegetationObject は、lod3MultiSurface 葉と幹？
                                try
                                {
                                    List<Building> newbuildings  = CreateTRANLOD3Multi(reader,fullPath);
                                    if (newbuildings != null) buildings.AddRange(newbuildings);
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine(ex.Message); // Parse error
                                }
                            }

                            if (reader.Name == "app:appearanceMember")
                            {
                                //textures = ParseTextureInfo(reader, buildings);
                                matDic = ParseMaterialInfoVEG(reader);                                
                            }
                            break;
                        default:
                            break;
                    }
                    iyd++;                    
                    if (iyd%100==0) {
                        yield return null;  
                    }
                    //Debug.Log("Time.time "+ Time.time+" sw.Elapsed "+ sw.Elapsed+" sw2.Elapsed "+ sw2.Elapsed); //経過時間);        
                   			float elapsed = (float)sw2.Elapsed.TotalSeconds;   
                    if (elapsed>0.3) {
                        sw2.Stop(); //計測終了
                        sw2.Restart();
                        sw2.Start();
                        yield return null;  
                    }                      
                }
                
            }
            if(textures != null)
            {
                // UVデータをビルデータにマージ
                MargeData(buildings, textures);
            }
            if (matDic != null) {
                MargeDataMat(buildings, matDic);
            }            
            //Debug.Log("GetTRANLOD3s ");////////            //return buildings.ToArray();
            cordata.cor1finished = true;
                sw.Stop(); //計測終了
                sw2.Stop(); //計測終了

            //List<Building> buildings = new List<Building>();
        }






        public Relief CreateRelief(XmlReader reader)
        {
            Relief building = new Relief();

            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList member = cd.ChildNodes;
            List<Surface> surfaces = new List<Surface>();

            foreach (XmlNode node in member)
            {
                if (node.Name == "gml:Triangle")
                {
                    Surface s = new Surface();
                    string posStr = node.InnerText;
                    s.SetPositions(Position.ParseString(posStr));
                    if(LowerCorner.Latitude < s.LowerCorner.Latitude && LowerCorner.Longitude < s.LowerCorner.Longitude
                        && s.UpperCorner.Latitude < UpperCorner.Latitude && s.UpperCorner.Longitude < UpperCorner.Longitude)
                    {
                        // 法線を反転する
                        Position p = s.Positions[2];
                        s.Positions[2] = s.Positions[1];
                        s.Positions[1] = p;
                        surfaces.Add(s);
                    }
                }

            }
            building.LOD1Solid = surfaces.ToArray();

            if (building.LOD1Solid != null)
            {
                (Position lower, Position upper) = GetCorner(building.LOD1Solid);
                building.LowerCorner = lower;
                building.UpperCorner = upper;
            }

            return building;
        }

        public Building CreateBuildingLOD3(XmlReader reader)
        {
            Building building = new Building();

            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList member = cd.ChildNodes;
            //Dictionary<string, Surface> surfaceDic = null;
            Dictionary<string, Surface> surfaceDicLOD3 = null;
            foreach (XmlNode node in member)
            {
                //Debug.Log(node.Name);
                if (node.Attributes["name"]?.Value == "建物ID")
                {
                    string id = node.InnerText;
                    building.Id = id;
                    Debug.Log("建物ID"+id);
                    if (id!="22203-bldg-48893") {
                    //    return null;
                    }
                    if (id!="22203-bldg-96740") { //7-11
                    //    return null;
                    }
                }
                if (node.Name == "gml:name")
                {
                    building.Name = node.FirstChild.Value;

                }
                if (node.Name == "bldg:measuredHeight")
                {
                    building.Height = Convert.ToSingle(node.FirstChild.Value);
                }
                if(node.Name== "bldg:lod0RoofEdge")
                {
                    building.LOD0RoofEdge = GetLOD0Surface(node);
                }
                if (node.Name== "bldg:lod3Solid")
                {
                    surfaceDicLOD3 = GetPolyList(node);
                }
            }
            //Debug.Log("end foreach1 " );
            //if (surfaceDic != null) Debug.Log(" LOD2 " + surfaceDic.Count);
            if (surfaceDicLOD3 != null) Debug.Log(" LOD3 " + surfaceDicLOD3.Count);
            //int count = 0;
            int countLOD3 = 0;
            foreach (XmlNode node in member)
            {
                if(node.Name== "bldg:boundedBy" && node.FirstChild.FirstChild.Name == "bldg:lod3MultiSurface")
                {
                    UpdateSurfaceDic(node,surfaceDicLOD3);
                    countLOD3++;
                    //Debug.Log(countLOD3);
                }                
                /*
                if(node.Name== "bldg:opening")
                {
                    UpdateSurfaceDic(node,surfaceDicLOD3);
                }

                ///////////////////////////////////////////////////////
                if (node.Name== "bldg:lod3Solid")
                {
                    surfaceDic = GetPolyListLOD3(node);
                }
                if(node.Name== "bldg:opening")
                {
                    UpdateSurfaceDic(node,surfaceDic);
                }
                ///////////////////////////////////////////////////////
                */
            }
            //Debug.Log("end foreach2 " + count +" "+ countLOD3 );
            if(surfaceDicLOD3 != null)
            {
                List<Surface> sList = new List<Surface>();
                foreach (var d in surfaceDicLOD3.Keys)
                {
                    var s = surfaceDicLOD3[d];
                    if (s != null && s.LowerCorner != Position.None)
                    {
                        sList.Add(s);
                    }
                }
                building.LOD2Solid = sList.ToArray();
                (Position lower, Position upper) = GetCorner(building.LOD2Solid);

                // モデルの min側の角が領域内に入っていれば採用
                // if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                //     && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                {
                    building.LowerCorner = lower;
                    building.UpperCorner = upper;
                }
                // else
                // {
                //     building.LOD2Solid = new Surface[] { };
                // }
            } 
            return building;
        }


        public Building CreateBuilding(XmlReader reader)
        {
            Building building = new Building();

            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList member = cd.ChildNodes;
            Dictionary<string, Surface> surfaceDic = null;
            Dictionary<string, Surface> surfaceDicLOD3 = null;
            foreach (XmlNode node in member)
            {
                //Debug.Log(node.Name);
                if (node.Attributes["name"]?.Value == "建物ID")
                {
                    string id = node.InnerText;
                    building.Id = id;
                    Debug.Log("建物ID"+id);
                    // if (id!="22203-bldg-48893") {
                    //     return null;
                    // }
                    // if (id!="22203-bldg-97256") { //5628 LOD3
                    //     return null;
                    // }
                }
                if (node.Name == "gml:name")
                {
                    building.Name = node.FirstChild.Value;

                }
                if (node.Name == "bldg:measuredHeight")
                {
                    building.Height = Convert.ToSingle(node.FirstChild.Value);
                }
                if(node.Name== "bldg:lod0RoofEdge")
                {
                    building.LOD0RoofEdge = GetLOD0Surface(node);
                }
                if (node.Name == "bldg:lod1Solid")
                {
                    building.LOD1Solid = GetLOD1Surface(node);
                }
                if (node.Name== "bldg:lod2Solid" )
                {
                    surfaceDic = GetPolyList(node);
                }
                // if (node.Name== "bldg:lod3Solid")
                // {
                //     surfaceDicLOD3 = GetPolyList(node);
                // }
            }
            //Debug.Log("end foreach1 " );
            if (surfaceDic != null) Debug.Log(" LOD2 " + surfaceDic.Count);
            if (surfaceDicLOD3 != null) Debug.Log(" LOD3 " + surfaceDicLOD3.Count);
            int count = 0;
            //int countLOD3 = 0;
            foreach (XmlNode node in member)
            {
                if(node.Name== "bldg:boundedBy" && node.FirstChild.FirstChild.Name == "bldg:lod2MultiSurface")
                {
                    UpdateSurfaceDic(node,surfaceDic);
                    count++;
                    //Debug.Log(count);
                }
                // if(node.Name== "bldg:boundedBy" && node.FirstChild.FirstChild.Name == "bldg:lod3MultiSurface")
                // {
                //     UpdateSurfaceDic(node,surfaceDicLOD3);
                //     countLOD3++;
                //     //Debug.Log(countLOD3);
                // }                
                /*
                if(node.Name== "bldg:opening")
                {
                    UpdateSurfaceDic(node,surfaceDicLOD3);
                }

                ///////////////////////////////////////////////////////
                if (node.Name== "bldg:lod3Solid")
                {
                    surfaceDic = GetPolyListLOD3(node);
                }
                if(node.Name== "bldg:opening")
                {
                    UpdateSurfaceDic(node,surfaceDic);
                }
                ///////////////////////////////////////////////////////
                */
            }
            //Debug.Log("end foreach2 " + count +" "+ countLOD3 );
            if(surfaceDicLOD3 != null)
            {
                List<Surface> sList = new List<Surface>();
                foreach (var d in surfaceDicLOD3.Keys)
                {
                    var s = surfaceDicLOD3[d];
                    if (s != null && s.LowerCorner != Position.None)
                    {
                        sList.Add(s);
                    }
                }
                building.LOD2Solid = sList.ToArray();
                (Position lower, Position upper) = GetCorner(building.LOD2Solid);

                // モデルの min側の角が領域内に入っていれば採用
                // if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                //     && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                {
                    building.LowerCorner = lower;
                    building.UpperCorner = upper;
                }
                // else
                // {
                //     building.LOD2Solid = new Surface[] { };
                // }
            } else // LOD2が指定されていない場合は null
            if(surfaceDic != null)
            {
                List<Surface> sList = new List<Surface>();
                foreach (var d in surfaceDic.Keys)
                {
                    var s = surfaceDic[d];
                    if (s != null && s.LowerCorner != Position.None)
                    {
                        sList.Add(s);
                    }
                }
                building.LOD2Solid = sList.ToArray();
                (Position lower, Position upper) = GetCorner(building.LOD2Solid);

                // モデルの min側の角が領域内に入っていれば採用
                // if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                //     && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                {
                    building.LowerCorner = lower;
                    building.UpperCorner = upper;
                }
                // else
                // {
                //     building.LOD2Solid = new Surface[] { };
                // }
            }
            if(building.LOD1Solid != null && building.LOD2Solid == null)
            {
                (Position lower, Position upper) = GetCorner(building.LOD1Solid);
                building.LowerCorner = lower;
                building.UpperCorner = upper;
            }

            return building;
        }



        public List<Building>  CreateFRN(XmlReader reader, string fullPath,bool frnSplit)
        {
            Building building = new Building();
            building.GmlPath = fullPath;
            List<Building> buildings = new List<Building>();
            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList member = cd.ChildNodes;
            Dictionary<string, Surface> surfaceDic = null;
            Dictionary<string, Surface> surfaceDicLOD3 = null;
            Debug.Log("CreateFRN "+cd.Name+"  gml:id "+cd.Attributes["gml:id"]?.Value);
            building.Id = cd.Attributes["gml:id"]?.Value;
            building.Name = building.Id;    
            bool split = true;        
            // if (building.Name!="FRN_8145ccfa-af0b-42c2-9598-64aba656a752"){// signal ランプ分割特殊
            //     return null;
            // }
            int splitsize = 18;// 分離する信号のランプの閾値　18
            foreach (XmlNode node in member)
            {
                Debug.Log(node.Name);
                if (node.Name== "frn:lod3Geometry")
                {
                    surfaceDicLOD3 = GetPolyListFRN(node);
                }
                if (node.Name== "frn:function")
                {
                    if (node.InnerText == "4900") { //signal
                        split = frnSplit;
                    } else {
                        split = false;
                    }
                    if (node.InnerText != "") {
                        try
                        {
                            int result = Int32.Parse(node.InnerText);
                            if (1000 <= result && result < 2000) {
                                building.nocollider = true;
                            }
                        }
                        catch (FormatException){ }
                    }
                    //Debug.Log("frn:function "+node.InnerText);
                }                
            }
            bool cutmode = false;
            int [] cutposition ={};
            int [][] cutpositions ={
               new int[] {742,770,928,956,1136,1164,1344,1372,1575,1603,2294,2322,2502,2530,2710,2738,2919,2947,3128,3156},
               new int[] {154,172,190,208,270,288},
               new int[] {412,440,620,648,828,856, 1338,1366,1546,1574,1754,1782},
               new int[] {154,172,190,208,270,288},
               new int[] {436,464,645,672,852,880},
               new int[] {160,178,196,214,276,294},
               new int[] {424,452,632,660,840,868,  1364,1392,1572,1600,1780,1808},
               new int[] {428,456,636,664,844,872},
               new int[] {154,172,190,208,270,288},
            };
            int cutindex = 0;
            string [] cutnames = {
                "FRN_8145ccfa-af0b-42c2-9598-64aba656a752",
                "FRN_0ec712e1-034c-48f8-bf46-a08aa948106d",
                "FRN_2ad1702c-e313-4df0-a73a-c589610f0223",
                "FRN_0a070f8f-9482-42d8-a666-fa81f7a5d686",
                "FRN_c320bc55-3b27-4192-aee9-a8e8f430c442",
                "FRN_c6938014-1deb-479f-a042-5d8d8ea2da0a",
                "FRN_a7274a10-e946-475b-8074-8e3a6ee98145",
                "FRN_028e51fe-d6ba-463b-8fa9-7af052efe729",
                "FRN_12cc2e74-eaef-47b9-b795-3ef563fc9337"
            };

            // if (!(building.Name=="FRN_8145ccfa-af0b-42c2-9598-64aba656a752" 
            //      ||    building.Name=="FRN_0ec712e1-034c-48f8-bf46-a08aa948106d" ||
            //          building.Name=="FRN_2ad1702c-e313-4df0-a73a-c589610f0223" ||
            //          building.Name=="FRN_0a070f8f-9482-42d8-a666-fa81f7a5d686" ||
            //          building.Name=="FRN_c320bc55-3b27-4192-aee9-a8e8f430c442" ||
            //          building.Name=="FRN_c6938014-1deb-479f-a042-5d8d8ea2da0a" ||
            //          building.Name=="FRN_a7274a10-e946-475b-8074-8e3a6ee98145" ||
            //          building.Name=="FRN_028e51fe-d6ba-463b-8fa9-7af052efe729" ||
            //          building.Name=="FRN_12cc2e74-eaef-47b9-b795-3ef563fc9337" 
            //     ))// signal ランプ分割特殊
            // {
            //     return null;
            // }
            for(int i = 0; i < cutnames.Length; i++) {
                if (building.Name==cutnames[i] ){
                    cutmode = true;
                    cutposition = cutpositions[i];
                    cutindex = 0;         
                    split = frnSplit;
                    splitsize = 0;                           
                    break;
                }
            }
            /* 742,770,928,956,1136,1164,1344,1372,
            1575,1603,2294,2322,2502,2530,2710,2738,
            2919,2947,3128,3156,

            0ec7
            154,172,190,208,270,288,

            2ad1
            412,440,620,648,828,856,
            1338,1366,1546,1574,1754,1782

            0a07
            154,172,190,208,270,288


            c320
            436,464,645,672,852,880,

            c693
            160,178,196,214,276,294,


            a727
            424,452,632,660,840,868,
            1364,1392,1572,1600,1780,1808

            028e
            428,456,636,664,844,872,

            12cc
            154,172,190,208,270,288,
            */
            // if (building.Name=="FRN_8145ccfa-af0b-42c2-9598-64aba656a752" ||
            //     building.Name=="FRN_0ec712e1-034c-48f8-bf46-a08aa948106d" ||
            //     building.Name=="FRN_2ad1702c-e313-4df0-a73a-c589610f0223" ||
            //     building.Name=="FRN_0a070f8f-9482-42d8-a666-fa81f7a5d686" ||
            //     building.Name=="FRN_c320bc55-3b27-4192-aee9-a8e8f430c442" ||
            //     building.Name=="FRN_c6938014-1deb-479f-a042-5d8d8ea2da0a" ||
            //     building.Name=="FRN_a7274a10-e946-475b-8074-8e3a6ee98145" ||
            //     building.Name=="FRN_028e51fe-d6ba-463b-8fa9-7af052efe729" ||
            //     building.Name=="FRN_12cc2e74-eaef-47b9-b795-3ef563fc9337" 
            //     )// signal ランプ分割特殊
            // {
            //     split = frnSplit;
            //     splitsize = 0;
            // }            
            //Debug.Log("end foreach1 " );
            if (surfaceDic != null) Debug.Log(" LOD2 " + surfaceDic.Count);
            if (surfaceDicLOD3 != null) Debug.Log(" LOD3 " + surfaceDicLOD3.Count);
            //int count = 0;
            int countLOD3 = 0;
            foreach (XmlNode node in member)
            {
                if(node.Name== "frn:lod3Geometry")
                {
                    UpdateSurfaceDicFRN(node,surfaceDicLOD3);
                    countLOD3++;
                    //Debug.Log(countLOD3);
                }                
                /*
                if(node.Name== "bldg:opening")
                {
                    UpdateSurfaceDic(node,surfaceDicLOD3);
                }

                ///////////////////////////////////////////////////////
                if (node.Name== "bldg:lod3Solid")
                {
                    surfaceDic = GetPolyListLOD3(node);
                }
                if(node.Name== "bldg:opening")
                {
                    UpdateSurfaceDic(node,surfaceDic);
                }
                ///////////////////////////////////////////////////////
                */
            }
            //Debug.Log("end foreach2 " + count +" "+ countLOD3 );
            if(surfaceDicLOD3 != null)
            {
                //Debug.Log("surfaceDicLOD3");
                List<Surface> sList = new List<Surface>();
                int number = 0;

                if (!cutmode) {
                    foreach (var d in surfaceDicLOD3.Keys)
                    {
                        var s = surfaceDicLOD3[d];
                        if (s != null && s.LowerCorner != Position.None)
                        {
                            //Debug.Log("s.Positions.Length "+s.Positions.Length);
                            if (split) {
                                if (s.Positions.Length >= splitsize) { // 分離する信号のランプの閾値　18
                                    if (sList.Count >0) {
                                        Building buildingS = new Building();
                                        buildingS.GmlPath = fullPath;
                                        buildingS.Id = building.Id+"#"+number.ToString("00000");  
                                        buildingS.Name = buildingS.Id;  
                                        buildingS.LOD2Solid = sList.ToArray();
                                        buildings.Add(buildingS);
                                        number++;
                                        sList = new List<Surface>();
                                    }
                                    Building buildingX = new Building();
                                    buildingX.GmlPath = fullPath;
                                    buildingX.Id = building.Id+"#"+number.ToString("00000");  
                                    buildingX.Name = buildingX.Id;  
                                    List<Surface> sListX = new List<Surface>();
                                    sListX.Add(s);
                                    buildingX.LOD2Solid = sListX.ToArray();
                                    buildings.Add(buildingX);
                                    number++;

                                } else {
                                    sList.Add(s);
                                }
                            } else {
                                sList.Add(s);
                            }
                        }
                    }
                }
                else
                { // 数では分離できないタイプ
                    foreach (var d in surfaceDicLOD3.Keys)
                    {
                        var s = surfaceDicLOD3[d];
                        if (s != null && s.LowerCorner != Position.None)
                        {
                            //Debug.Log("s.Positions.Length "+s.Positions.Length);
                            Debug.Log("number "+number);
                                if (cutindex < cutposition.Length && number == cutposition[cutindex] ) { // 分離する信号のランプの閾値　18
                                    Debug.Log("cut");
                                    cutindex++;
                                    if (sList.Count >0) {
                                        Debug.Log("Add");
                                        Building buildingS = new Building();
                                        buildingS.GmlPath = fullPath;
                                        buildingS.Id = building.Id+"#"+number.ToString("00000");  
                                        buildingS.Name = buildingS.Id;  
                                        buildingS.LOD2Solid = sList.ToArray();

                                        buildings.Add(buildingS);
                                        sList = new List<Surface>();
                                        sList.Add(s);
                                    }
                                    // Building buildingX = new Building();
                                    // buildingX.GmlPath = fullPath;
                                    // buildingX.Id = building.Id+"#"+number.ToString("00000");  
                                    // buildingX.Name = buildingX.Id;  
                                    // List<Surface> sListX = new List<Surface>();
                                    // sListX.Add(s);
                                    // buildingX.LOD2Solid = sListX.ToArray();
                                    // buildings.Add(buildingX);
                                    // number++;

                                } else {
                                    sList.Add(s);
                                }
                                number++;

                        }
                    }
                    Debug.Log("Count "+buildings.Count);

                }
                if (sList.Count >0) {
                
                    if (split) {
                        building.Id = building.Id+"#"+number.ToString("00000");  
                        building.Name = building.Id;  
                    }
                    building.LOD2Solid = sList.ToArray();
                    (Position lower, Position upper) = GetCorner(building.LOD2Solid);
                    // モデルの min側の角が領域内に入っていれば採用
                    // if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                    //     && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                    {
                        building.LowerCorner = lower;
                        building.UpperCorner = upper;
                    }
                    // else
                    // {
                    //     building.LOD2Solid = new Surface[] { };
                    // }
                    buildings.Add(building);
                }
            } 
            // else // LOD2が指定されていない場合は null
            // if(surfaceDic != null)
            // {
            //     List<Surface> sList = new List<Surface>();
            //     foreach (var d in surfaceDic.Keys)
            //     {
            //         var s = surfaceDic[d];
            //         if (s != null && s.LowerCorner != Position.None)
            //         {
            //             sList.Add(s);
            //         }
            //     }
            //     building.LOD2Solid = sList.ToArray();
            //     (Position lower, Position upper) = GetCorner(building.LOD2Solid);

            //     // モデルの min側の角が領域内に入っていれば採用
            //     if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
            //         && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
            //     {
            //         building.LowerCorner = lower;
            //         building.UpperCorner = upper;
            //     }
            //     else
            //     {
            //         building.LOD2Solid = new Surface[] { };
            //     }
            // }
            // if(building.LOD1Solid != null && building.LOD2Solid == null)
            // {
            //     (Position lower, Position upper) = GetCorner(building.LOD1Solid);
            //     building.LowerCorner = lower;
            //     building.UpperCorner = upper;
            // }
            //     Debug.Log(building.LOD2Solid.Length);
            
            return buildings;
        }
/*

        public Building CreateVEG(XmlReader reader)
        {
            Building building = new Building();

            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList member = cd.ChildNodes;
            Dictionary<string, Surface> surfaceDic = null;
            Dictionary<string, Surface> surfaceDicLOD3 = null;
            Debug.Log("CreateVEG "+cd.Name+"  gml:id "+cd.Attributes["gml:id"]?.Value);
            building.Id = cd.Attributes["gml:id"]?.Value;
            building.Name = building.Id;            
            foreach (XmlNode node in member)
            {
                Debug.Log(node.Name);
                if (node.Name== "veg:lod3Geometry"|| node.Name== "veg:lod3MultiSurface")
                {
                    surfaceDicLOD3 = GetPolyListVEG(node);
                }
            }
            Debug.Log("end foreach1 " );
            if (surfaceDic != null) Debug.Log(" LOD2 " + surfaceDic.Count);
            if (surfaceDicLOD3 != null) Debug.Log(" LOD3 " + surfaceDicLOD3.Count);
            int count = 0;
            int countLOD3 = 0;
            foreach (XmlNode node in member)
            {
                if(node.Name== "veg:lod3Geometry" || node.Name== "veg:lod3MultiSurface" )
                {
                    UpdateSurfaceDicVEG(node,surfaceDicLOD3);
                    countLOD3++;
                    Debug.Log(countLOD3);
                }                

            }
            Debug.Log("end foreach2 " + count +" "+ countLOD3 );
                        if(surfaceDicLOD3 != null)
            {
                Debug.Log("surfaceDicLOD3");
                List<Surface> sList = new List<Surface>();
                foreach (var d in surfaceDicLOD3.Keys)
                {
                    var s = surfaceDicLOD3[d];
                    if (s != null && s.LowerCorner != Position.None)
                    {
                        sList.Add(s);
                    }
                }
                building.LOD2Solid = sList.ToArray();
                (Position lower, Position upper) = GetCorner(building.LOD2Solid);
                // モデルの min側の角が領域内に入っていれば採用
                if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                    && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                {
                    building.LowerCorner = lower;
                    building.UpperCorner = upper;
                }
                else
                {
                    building.LOD2Solid = new Surface[] { };
                }
            } else // LOD2が指定されていない場合は null
            if(surfaceDic != null)
            {
                List<Surface> sList = new List<Surface>();
                foreach (var d in surfaceDic.Keys)
                {
                    var s = surfaceDic[d];
                    if (s != null && s.LowerCorner != Position.None)
                    {
                        sList.Add(s);
                    }
                }
                building.LOD2Solid = sList.ToArray();
                (Position lower, Position upper) = GetCorner(building.LOD2Solid);

                // モデルの min側の角が領域内に入っていれば採用
                if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                    && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                {
                    building.LowerCorner = lower;
                    building.UpperCorner = upper;
                }
                else
                {
                    building.LOD2Solid = new Surface[] { };
                }
            }
            if(building.LOD1Solid != null && building.LOD2Solid == null)
            {
                (Position lower, Position upper) = GetCorner(building.LOD1Solid);
                building.LowerCorner = lower;
                building.UpperCorner = upper;
            }
                Debug.Log(building.LOD2Solid.Length);

            return building;
        }


        public List<Building> CreateVEGMultiOLD(XmlReader reader, string fullPath)
        {
            List<Building> buildings = new List<Building>();

            Building building = new Building();
            building.GmlPath = fullPath;
            
            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList member = cd.ChildNodes;
            Dictionary<string, Surface> surfaceDic = null;
            Dictionary<string, Surface> surfaceDicLOD3 = null;
            Debug.Log("CreateVEG "+cd.Name+"  gml:id "+cd.Attributes["gml:id"]?.Value);
            building.Id = cd.Attributes["gml:id"]?.Value;
            building.Name = building.Id;            
            foreach (XmlNode node in member)
            {
                Debug.Log(node.Name);
                if (node.Name== "veg:lod3Geometry"|| node.Name== "veg:lod3MultiSurface")
                {
                    surfaceDicLOD3 = GetPolyListVEG(node);
                }
            }
            Debug.Log("end foreach1 " );
            if (surfaceDic != null) Debug.Log(" LOD2 " + surfaceDic.Count);
            if (surfaceDicLOD3 != null) Debug.Log(" LOD3 " + surfaceDicLOD3.Count);
            int count = 0;
            int countLOD3 = 0;
            foreach (XmlNode node in member)
            {
                if(node.Name== "veg:lod3Geometry" || node.Name== "veg:lod3MultiSurface" )
                {
                    UpdateSurfaceDicVEG(node,surfaceDicLOD3);
                    countLOD3++;
                    Debug.Log(countLOD3);
                }                
            }
            Debug.Log("end foreach2 " + count +" "+ countLOD3 );
                        if(surfaceDicLOD3 != null)
            {
                Debug.Log("surfaceDicLOD3");
                List<Surface> sList = new List<Surface>();
                foreach (var d in surfaceDicLOD3.Keys)
                {
                    var s = surfaceDicLOD3[d];
                    if (s != null && s.LowerCorner != Position.None)
                    {
                        sList.Add(s);
                    }
                }
                building.LOD2Solid = sList.ToArray();
                (Position lower, Position upper) = GetCorner(building.LOD2Solid);
                // モデルの min側の角が領域内に入っていれば採用
                if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                    && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                {
                    building.LowerCorner = lower;
                    building.UpperCorner = upper;
                }
                else
                {
                    building.LOD2Solid = new Surface[] { };
                }
            } else // LOD2が指定されていない場合は null
            if(surfaceDic != null)
            {
                List<Surface> sList = new List<Surface>();
                foreach (var d in surfaceDic.Keys)
                {
                    var s = surfaceDic[d];
                    if (s != null && s.LowerCorner != Position.None)
                    {
                        sList.Add(s);
                    }
                }
                building.LOD2Solid = sList.ToArray();
                (Position lower, Position upper) = GetCorner(building.LOD2Solid);

                // モデルの min側の角が領域内に入っていれば採用
                if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                    && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                {
                    building.LowerCorner = lower;
                    building.UpperCorner = upper;
                }
                else
                {
                    building.LOD2Solid = new Surface[] { };
                }
            }
            if(building.LOD1Solid != null && building.LOD2Solid == null)
            {
                (Position lower, Position upper) = GetCorner(building.LOD1Solid);
                building.LowerCorner = lower;
                building.UpperCorner = upper;
            }
                Debug.Log(building.LOD2Solid.Length);
            buildings.Add(building);
            return buildings;
        }
*/

        public List<Building> CreateVEGMulti(XmlReader reader, string fullPath)
        {
            //if (reader.Name == "veg:PlantCover" || reader.Name == "veg:SolitaryVegetationObject")
            List<Building> buildings = new List<Building>();


            
            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList member = cd.ChildNodes;
            Dictionary<string, Surface> surfaceDicLOD3 = null;
            Debug.Log("CreateVEG "+cd.Name+"  gml:id "+cd.Attributes["gml:id"]?.Value);

            //if (cd.Attributes["gml:id"]?.Value != "VEG_063bb4fb-b6e8-4e5a-9ec2-57e0ecef785d") return null;
            foreach (XmlNode node1 in member)
            {
                //Debug.Log(node1.Name);
                if (node1.Name== "veg:lod3Geometry"|| node1.Name== "veg:lod3MultiSurface")
                {
                    //Debug.Log("node1.FirstChild.ChildNodes "+node1.FirstChild.ChildNodes.Count);
                    foreach (XmlNode node in node1.FirstChild.ChildNodes)
                    {
                        Building building = new Building();
                        building.GmlPath = fullPath;
                        building.Id = cd.Attributes["gml:id"]?.Value+"="+node.FirstChild.Attributes["gml:id"].Value;
                        building.Name = building.Id;    
                        //Debug.Log("building.Id" + building.Id);
                        surfaceDicLOD3 = GetSurfaceDicVEG(node);
                        if(surfaceDicLOD3 != null)
                        {
                            //Debug.Log("surfaceDicLOD3");
                            List<Surface> sList = new List<Surface>();
                            foreach (var d in surfaceDicLOD3.Keys)
                            {
                                var s = surfaceDicLOD3[d];
                                if (s != null && s.LowerCorner != Position.None)
                                {
                                    sList.Add(s);
                                }
                            }
                            building.LOD2Solid = sList.ToArray();
                            (Position lower, Position upper) = GetCorner(building.LOD2Solid);
                            // モデルの min側の角が領域内に入っていれば採用
                            // if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                            //     && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                            {
                                building.LowerCorner = lower;
                                building.UpperCorner = upper;
                                //Debug.Log(building.LOD2Solid.Length);
                                buildings.Add(building);
                            }
                        } 
                    }
                }
            }

            return buildings;
        }

        public List<Building> CreateTRANLOD3Multi(XmlReader reader, string fullPath)
        {
            //if (reader.Name == "tran:Road")
            List<Building> buildings = new List<Building>();


            
            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList member = cd.ChildNodes;
            Dictionary<string, Surface> surfaceDicLOD3 = null;
            Debug.Log("CreateTRANLOD3Multi "+cd.Name+"  gml:id "+cd.Attributes["gml:id"]?.Value);



            //if (cd.Attributes["gml:id"]?.Value != "VEG_063bb4fb-b6e8-4e5a-9ec2-57e0ecef785d") return null;
            foreach (XmlNode node2 in member)
            {
                //Debug.Log("node2 "+node2.Name);
                    foreach (XmlNode node1 in node2.FirstChild.ChildNodes)// tran:lod3MultiSurface
                    {
                        //Debug.Log("node1 "+node1.Name); 

                                    string id2 = "";
                                    Building building = new Building();
                                    building.GmlPath = fullPath;


                                                                            List<Surface> sList = new List<Surface>();


                        if (node1.Name== "tran:lod3MultiSurface")
                        {
                            //Debug.Log("node1.FirstChild.ChildNodes "+node1.FirstChild.ChildNodes.Count);
                            foreach (XmlNode node0 in node1.FirstChild.FirstChild.ChildNodes)///CompositeSurface
                            {
                                foreach (XmlNode node in node0.ChildNodes)///SurfaceMember
                                {
                                    // Building building = new Building();
                                    // building.GmlPath = fullPath;
                                    // building.Id = cd.Attributes["gml:id"]?.Value+"="+node.FirstChild.Attributes["gml:id"].Value;
                                    // building.Name = building.Id;    
                                    // Debug.Log("building.Id" + building.Id);
                                    id2 = node.FirstChild.Attributes["gml:id"].Value;
                                    surfaceDicLOD3 = GetSurfaceDicTRANLOD3(node);
                                    if(surfaceDicLOD3 != null)
                                    {
                                        //Debug.Log("surfaceDicLOD3");
                                        // List<Surface> sList = new List<Surface>();
                                        foreach (var d in surfaceDicLOD3.Keys)
                                        {
                                            var s = surfaceDicLOD3[d];
                                            if (s != null && s.LowerCorner != Position.None)
                                            {
                                                sList.Add(s);
                                            }
                                        }
                                    //     building.LOD2Solid = sList.ToArray();
                                    //     (Position lower, Position upper) = GetCorner(building.LOD2Solid);
                                    //     // モデルの min側の角が領域内に入っていれば採用
                                    // //if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                                    // //     && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                                    //     {
                                    //         building.LowerCorner = lower;
                                    //         building.UpperCorner = upper;
                                    //         Debug.Log(building.LOD2Solid.Length);
                                    //         buildings.Add(building);
                                    //     }
                                    } 
                                }
                            }
                        }
                                    building.Id = cd.Attributes["gml:id"]?.Value+"="+id2;//node.FirstChild.Attributes["gml:id"].Value;
                                    building.Name = building.Id;    
                                    //Debug.Log("building.Id" + building.Id);            
                                        building.LOD2Solid = sList.ToArray();
                                        (Position lower, Position upper) = GetCorner(building.LOD2Solid);
                                        // モデルの min側の角が領域内に入っていれば採用
                                    //if ( LowerCorner.Latitude < lower.Latitude && upper.Latitude < UpperCorner.Latitude
                                    //     && LowerCorner.Longitude < lower.Longitude && upper.Longitude < UpperCorner.Longitude)
                                        {
                                            building.LowerCorner = lower;
                                            building.UpperCorner = upper;
                                            //Debug.Log(building.LOD2Solid.Length);
                                            buildings.Add(building);
                                        }


                    }
            }

            return buildings;
        }



        private (Position Lower, Position Upper) GetCorner(Surface[] surfaces)
        {
            double lLat = double.MaxValue;
            double lLon = double.MaxValue;
            double lAlt = double.MaxValue;
            double uLat = double.MinValue;
            double uLon = double.MinValue;
            double uAlt = double.MinValue;
            foreach (var s in surfaces)
            {
                if(s.LowerCorner != null)
                {
                    if (s.LowerCorner.Latitude < lLat) lLat = s.LowerCorner.Latitude;
                    if (s.LowerCorner.Longitude < lLon) lLon = s.LowerCorner.Longitude;
                    if (s.LowerCorner.Altitude < lAlt) lAlt = s.LowerCorner.Altitude;
                    if (s.UpperCorner.Latitude > uLat) uLat = s.UpperCorner.Latitude;
                    if (s.UpperCorner.Longitude > uLon) uLon = s.UpperCorner.Longitude;
                    if (s.UpperCorner.Altitude > uAlt) uAlt = s.UpperCorner.Altitude;
                }
            }
            return (new Position(lLat, lLon, lAlt), new Position(uLat, uLon, uAlt));
        }
        public Surface GetLOD0Surface(XmlNode node)
        {
            var s = node.FirstChild.FirstChild.FirstChild.FirstChild.FirstChild.FirstChild.FirstChild;
            Surface surface = new Surface();
            string posStr = s.Value;
            surface.SetPositions(Position.ParseString(posStr));
            return surface;
        }

        public Surface[] GetLOD1Surface(XmlNode node)
        {
            List<Surface> surfaces = new List<Surface>();
            // 多角形の名前のリストを取得
            XmlNodeList list = node.FirstChild.FirstChild.FirstChild.ChildNodes;
            for (int i = 0; i < list.Count; i++)
            {
                Surface s = new Surface();
                string posStr = list[i].FirstChild.FirstChild.FirstChild.FirstChild.FirstChild.Value;

                s.SetPositions(Position.ParseString(posStr));
                if (LowerCorner.Latitude < s.LowerCorner.Latitude && LowerCorner.Longitude < s.LowerCorner.Longitude
                    && s.UpperCorner.Latitude < UpperCorner.Latitude && s.UpperCorner.Longitude < UpperCorner.Longitude)
                {
                    surfaces.Add(s);
                }
            }
            return surfaces.ToArray();
        }

        private void UpdateSurfaceDic(XmlNode node, Dictionary<string, Surface> polyDic)
        {
            //Debug.Log("F "+node.FirstChild.Name);
            //Debug.Log("FF "+node.FirstChild.FirstChild.Name);
            // 名前に対応する頂点リストを取得する
            XmlNode n = node.FirstChild.FirstChild.FirstChild?.FirstChild?.FirstChild;
            string xml = node.InnerXml.Replace("gml:","");
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList s = doc.SelectNodes("//surfaceMember");
            //Debug.Log("UpdateSurfaceDic "+s+" "+s.Count);
            foreach (XmlNode member in s)
            {
                var m2 = member.FirstChild;
                string name = m2.Attributes["id"].Value;
                //Debug.Log("UpdateSurfaceDic "+name);
                XmlNode p = m2.FirstChild.FirstChild.FirstChild.FirstChild;
                //Debug.Log("1");
                Position[] positions = Position.ParseString(p.Value);
                //Debug.Log("2");
                polyDic[name].SetPositions(positions);
                //Debug.Log("next");
            }
            //Debug.Log("UpdateSurfaceDic end");
        }
        private void UpdateSurfaceDicFRN(XmlNode node, Dictionary<string, Surface> polyDic)
        {
            //Debug.Log("F "+node.FirstChild.Name);
            //Debug.Log("FF "+node.FirstChild.FirstChild.Name);
            // 名前に対応する頂点リストを取得する
            //XmlNode n = node.FirstChild.FirstChild.FirstChild?.FirstChild?.FirstChild;
            string xml = node.InnerXml.Replace("gml:","");
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList s = doc.SelectNodes("//surfaceMember");
            //Debug.Log("UpdateSurfaceDic "+s+" "+s.Count);
            foreach (XmlNode member in s)
            {
                var m2 = member.FirstChild;
                string name = m2.Attributes["id"].Value;
                //Debug.Log("UpdateSurfaceDic "+name);
                XmlNode p = m2.FirstChild.FirstChild.FirstChild.FirstChild;
                //Debug.Log("1");
                Position[] positions = Position.ParseString(p.Value);
                //Debug.Log("2");
                polyDic[name].SetPositions(positions);
                //Debug.Log("next");
            }
            //Debug.Log("UpdateSurfaceDic end");
        }
        private void UpdateSurfaceDicVEG(XmlNode node, Dictionary<string, Surface> polyDic)
        {
            //Debug.Log("F "+node.FirstChild.Name);
            //Debug.Log("FF "+node.FirstChild.FirstChild.Name);
            // 名前に対応する頂点リストを取得する
            //XmlNode n = node.FirstChild.FirstChild.FirstChild?.FirstChild?.FirstChild;
            node = node.FirstChild.FirstChild;
            string xml = node.InnerXml.Replace("gml:","");
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList s = doc.SelectNodes("//surfaceMember");
            //Debug.Log("UpdateSurfaceDic "+s+" "+s.Count);
            foreach (XmlNode member in s)
            {
                var m2 = member.FirstChild;
                string name = m2.Attributes["id"].Value;
                //Debug.Log("UpdateSurfaceDic "+name);
                XmlNode p = m2.FirstChild.FirstChild.FirstChild.FirstChild;
                //Debug.Log("1");
                Position[] positions = Position.ParseString(p.Value);
                //Debug.Log("2");
                polyDic[name].SetPositions(positions);
                //Debug.Log("next");
            }
            //Debug.Log("UpdateSurfaceDicVEG end");
        }

        private  Dictionary<string, Surface> GetSurfaceDicVEG(XmlNode node)
        {
            //Debug.Log("node.name "+node.Name);
            //Debug.Log("node.child.name "+node.FirstChild.Name);            
            //Debug.Log("node.child.id "+node.FirstChild.Attributes["gml:id"].Value);            
            //Debug.Log("F "+node.FirstChild.Name);
            //Debug.Log("FF "+node.FirstChild.FirstChild.Name);
            // 名前に対応する頂点リストを取得する
            //XmlNode n = node.FirstChild.FirstChild.FirstChild?.FirstChild?.FirstChild;
            Dictionary<string, Surface> polyDic = new Dictionary<string, Surface>();

            //node = node.FirstChild.FirstChild;
            string xml = node.InnerXml.Replace("gml:","");
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList s = doc.SelectNodes("//surfaceMember");
            //Debug.Log("UpdateSurfaceDic Count "+s+" "+s.Count);
            foreach (XmlNode member in s)
            {
                var m2 = member.FirstChild;
                string name = m2.Attributes["id"].Value;
                //Debug.Log("UpdateSurfaceDic "+name);
                XmlNode p = m2.FirstChild.FirstChild.FirstChild.FirstChild;
                //Debug.Log("1");
                Position[] positions = Position.ParseString(p.Value);
                //Debug.Log("2");
                polyDic.Add(name, new Surface{Id = name });
                polyDic[name].SetPositions(positions);
                //Debug.Log("next");
            }
            //Debug.Log("UpdateSurfaceDicVEG end");
            return polyDic;
        }


        private  Dictionary<string, Surface> GetSurfaceDicTRANLOD3(XmlNode node)
        {
            //Debug.Log("node.name "+node.Name); // surfacemember
            //Debug.Log("node.child.name "+node.FirstChild.Name); //gml:CompositeSurface           
            //Debug.Log("node.child.id "+node.FirstChild.Attributes["gml:id"].Value); //gml:CompositeSurface gml:id=           
            //Debug.Log("F "+node.FirstChild.Name);
            //Debug.Log("FF "+node.FirstChild.FirstChild.Name);
            // 名前に対応する頂点リストを取得する
            //XmlNode n = node.FirstChild.FirstChild.FirstChild?.FirstChild?.FirstChild;
            Dictionary<string, Surface> polyDic = new Dictionary<string, Surface>();

            //node = node.FirstChild.FirstChild;
            string xml = node.InnerXml.Replace("gml:","");
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList s = doc.SelectNodes("//Polygon");
            //Debug.Log("UpdateSurfaceDic Count "+s+" "+s.Count);
            foreach (XmlNode member in s)
            {
                var m2 = member;//.FirstChild;
                string name = m2.Attributes["id"].Value;
                //Debug.Log("UpdateSurfaceDic "+name);
                XmlNode p = m2.FirstChild.FirstChild;
                //Debug.Log("1 "+ p.Name+" "+p.Value+" InnerText "+p.InnerText);
                Position[] positions = Position.ParseString(p.InnerText);
                //Debug.Log("2");
                polyDic.Add(name, new Surface{Id = name });
                polyDic[name].SetPositions(positions);
                //Debug.Log("next");
            }
            //Debug.Log("UpdateSurfaceDicVEG end");
            return polyDic;
        }


        public Dictionary<string, Surface> GetPolyList(XmlNode node)
        {
            Dictionary<string, Surface> dic = new Dictionary<string, Surface>();

            // 多角形の名前のリストを取得
            XmlNodeList list = node.FirstChild.FirstChild.FirstChild.ChildNodes;
            for (int i = 0; i < list.Count; i++)
            {
                string name = list[i].Attributes["xlink:href"].Value.Substring(1);
                dic.Add(name, new Surface{Id = name });
            }
            return dic;
        }
        public Dictionary<string, Surface> GetPolyListLOD3(XmlNode node)
        {
            Dictionary<string, Surface> dic = new Dictionary<string, Surface>();
            //Debug.Log("GetPolyListLOD3");
            // 多角形の名前のリストを取得
            XmlNodeList list = node.FirstChild.FirstChild.FirstChild.ChildNodes;
            for (int i = 0; i < list.Count; i++)
            {
                string name = list[i].Attributes["xlink:href"].Value.Substring(1);
                dic.Add(name, new Surface{Id = name });
                //Debug.Log("LOD3 "+name);
            }
            return dic;
        }
        public Dictionary<string, Surface> GetPolyListFRN(XmlNode node)
        {
            Dictionary<string, Surface> dic = new Dictionary<string, Surface>();
            XmlNodeList list = null;
            if (node.FirstChild.Name == "gml:CompositeSurface") {
                list = node.FirstChild.ChildNodes;//<gml:surfaceMember>
            } else if (node.FirstChild?.FirstChild?.FirstChild?.Name == "gml:CompositeSurface") {
                list = node.FirstChild?.FirstChild?.FirstChild?.ChildNodes;//<gml:surfaceMember>
            }  else {
                Debug.Log("Error ");
                return null;
            }
            // 多角形の名前のリストを取得
            for (int i = 0; i < list.Count; i++)
            {
                string name = list[i].FirstChild.Attributes["gml:id"].Value;//.Substring(1);
                dic.Add(name, new Surface{Id = name });
                //Debug.Log(name);
            }

            return dic;
        }       
        public Dictionary<string, Surface> GetPolyListVEG(XmlNode node)
        {
            Dictionary<string, Surface> dic = new Dictionary<string, Surface>();
            XmlNodeList list = null;
            if (node.FirstChild.Name == "gml:CompositeSurface") {
                list = node.FirstChild.ChildNodes;//<gml:surfaceMember>
            } else if (node.FirstChild?.FirstChild?.FirstChild?.Name == "gml:CompositeSurface") {
                list = node.FirstChild?.FirstChild?.FirstChild?.ChildNodes;//<gml:surfaceMember>
            }  else {
                Debug.Log("Error ");
                return null;
            }
            // 多角形の名前のリストを取得
            for (int i = 0; i < list.Count; i++)
            {
                string name = list[i].FirstChild.Attributes["gml:id"].Value;//.Substring(1);
                dic.Add(name, new Surface{Id = name });
                //Debug.Log(name);
            }

            return dic;
        }


        private TextureInfo ParseTextureInfo(XmlReader reader, List<Building> buildings)
        {

            var map = new Dictionary<string, (int index, Vector2[] uv)>();
            List<string> textureFiles = new List<string>();

            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList list = cd.FirstChild.ChildNodes;
            int count = list.Count;
            //Debug.Log("ParseTextureInfo "+count);
            for(int i = 0;i < count; i++) {
                if (list[i].Name == "app:surfaceDataMember") // 1枚のテクスチャに紐づくUV
                {
                    if (list[i].FirstChild.Name == "app:ParameterizedTexture"){
//                        Debug.Log(i+" "+list[i].Name);
                        XmlNodeList uv = list[i].FirstChild.ChildNodes;
                        for(int j=0; j<uv.Count; j++)
                        {
                            if (uv[j].Name == "app:target")
                            {
                                // UVデータを登録
                                string uri = uv[j]?.Attributes["uri"]?.Value;
                                if (uri==null) {
                                    return null;
                                }
                                uri = uri.Substring(1);
                                string texString = uv[j].FirstChild.FirstChild.FirstChild.Value;
                                map.Add(uri,(textureFiles.Count-1, ConvertToUV(texString)));
                                continue;
                            }

                            if (uv[j].Name == "app:imageURI")
                            {
                                string file = uv[j].FirstChild.Value;
                                textureFiles.Add(file);
                                //Debug.Log("URI "+file);
                                continue;
                            }
                        }
                    }

                }
            }
            //Debug.Log("return");
//            return null;
            return new TextureInfo { Files = textureFiles, Map = map };
        }


        private  Dictionary<string, Color> ParseMaterialInfoVEG(XmlReader reader)
        {
            Dictionary<string, Color>  matDic = new Dictionary<string, Color> ();
            XmlDocument doc = new XmlDocument();
            var r2 = reader.ReadSubtree();
            XmlNode cd = doc.ReadNode(r2);
            XmlNodeList list = cd.FirstChild.ChildNodes;
            List<string> textureFiles = new List<string>();
            //Debug.Log("reader.Name"+reader.Name);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Name == "app:surfaceDataMember") // 1枚のテクスチャに紐づくUV
                {
                    XmlNodeList node = list[i].FirstChild.ChildNodes;
                    Color c = new Color();
                    for(int j=0; j<node.Count; j++)
                    {
                        //Debug.Log("node[j].Name " +j+" "+node[j].Name);
                        if (node[j].Name == "app:diffuseColor")
                        {
                            //Debug.Log(node[j].InnerText);
                            string[] items = node[j].InnerText.Split(' ');
                            if (items.Length == 3) {
                                c = new Color(float.Parse(items[0]),float.Parse(items[1]),float.Parse(items[2]),1);
                            }
                        }                        
                        if (node[j].Name == "app:target")
                        {
                            string name = node[j].InnerText.Substring(1);
                            //Debug.Log("app:target "+name +"  "+c);
                            if (c != null && !matDic.ContainsKey(name)) {
                                
                                matDic.Add(name, c);
                            }
                        }
                    }
                }
            }
            return matDic;
        }



        private Vector2[] ConvertToUV(string uvText)
        {
            string[] items = uvText.Split(' ');
            int len = items.Length / 2 - 1; // 元の点列は始点と終点が同じ値なので終点を無視
            Vector2[] list = new Vector2[len];
            for (int i = 0; i < len; i++) 
            {
                list[i] = new Vector2
                {
                    x = Convert.ToSingle(items[i * 2]),
                    y = Convert.ToSingle(items[i * 2 + 1]),
                };
            }
            return list;
        }
        private void MargeData(List<Building> buildings, TextureInfo textureInfo)
        {
            var map = textureInfo.Map;
            var textureFiles = textureInfo.Files;
            foreach(var b in buildings)
            {
                if(b.LOD2Solid == null)
                {
                    continue;
                }
                var data = new List<(int Index, Vector2[] UV)>();
                for (int i=0; i<b.LOD2Solid.Length; i++)
                {
                    // あるビルのポリゴンのIDに一致するテクスチャがあったら割り当てる
                    if (map.ContainsKey(b.LOD2Solid[i].Id))
                    {
                        var d = map[b.LOD2Solid[i].Id];
                        b.LOD2Solid[i].UVs = d.uv;
                        data.Add(d);
                    }
                }
                //bool singleTexture = true;
                //for(int i=1; i<data.Count; i++)
                //{
                //    if(data[0].Index != data[i].Index)
                //    {
                //        singleTexture = false;
                //        break;
                //    }
                //}

                // テクスチャファイルが複数指定されている場合、実態としては同じ画像なので
                // 最初のテクスチャファイルを割り当てる
                // * singleTexture チェックを無視

                for (int i = 0; i < data.Count; i++)
                {
                    string key = b.LOD2Solid[i].Id;
                    if (map.ContainsKey(key))
                    {
                        b.LOD2Solid[i].TextureFile = textureFiles[data[0].Index];
                    }
                }
            }
        }
        private void RemoveNoTextureData(List<Building> buildings)
        {
            //foreach(var b in buildings)
            for(int j = buildings.Count -1 ; j >= 0; j--)
            {
                Building b = buildings[j];
                if(b.LOD2Solid == null)
                {
                    continue;
                }
                bool allno = true;
                for (int i = 0; i < b.LOD2Solid.Length; i++)
                {
                    if (b.LOD2Solid[i].TextureFile != null) {
                        allno = false;
                        break;
                    }
                }
                if (allno) {
                        //Debug.Log("RemoveNoTextureData");
                        buildings.RemoveAt(j);
                }
            }            
        }
        private void MargeDataMat(List<Building> buildings, Dictionary<string, Color> matDic)
        {
            //Debug.Log("MargeDataMat "+ matDic.Count);
            foreach(var b in buildings)
            {
                string [] id2 = b.Id.Split("=");
                //Debug.Log("b.Id "+b.Id+ " "+id2[1]);
                if (matDic.ContainsKey(id2[1]))
                {
                    b.color = matDic[id2[1]];
                    //Debug.Log("Color "+ id2[1]+" "+b.color);
                } else {
                    //Debug.Log("Color not found "+ id2[1]);
                }            
                
                // if(b.LOD2Solid == null)
                // {
                //     continue;
                // }
                // for (int i=0; i<b.LOD2Solid.Length; i++)
                // {
                //     Debug.Log("b.LOD2Solid.Length "+b.LOD2Solid.Length);
                //     if (matDic.ContainsKey(b.LOD2Solid[i].Id))
                //     {
                //         b.color = matDic[b.LOD2Solid[i].Id];
                //         Debug.Log("Color "+ b.LOD2Solid[i].Id+" "+b.color);
                //     }
                // }
            }
        }


    }
}
#endif