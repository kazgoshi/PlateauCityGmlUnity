using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Net;
using System.Collections;

using PlateauCityGml;

#if UNITY_EDITOR
using UnityEditor;
using Unity.EditorCoroutines.Editor;
  
/*
kaz54

PlateauCityGml
https://github.com/ksasao/PlateauCityGmlSharp
をベースにUnityで直接CityGMLファイルを読み込んでオブジェクト生成

2023/02/18 沼津のLOD3に対応、Coroutineを使用し中断を可能に
2024/02/10 大阪のLOD3に対応、

rod2の道路は高さの情報がないので地面の高さで高さを取得しているため、
生成時は、建物などがあれば削除する必要がある。

MeshやTextureなどは生成時にResourcesフォルダに保存しているため、
形状などを変更した場合は、フォルダごと削除がよい。

飯塚市は、demもtranもbldgも8桁の３次メッシュコード
呉市は、demとbldgが8桁の３次メッシュコード、tranは6桁の２次メッシュコード
福岡市は、demが6桁の２次メッシュコード(一部分割00,50,05,55)、bldgとtranは8桁の３次メッシュコード
大阪は、demが6桁の２次メッシュコード(一部分割00,50,05,55)、bldgとtranは8桁の３次メッシュコード

大阪
51357392_tran_6697_op.gmlの
tran_d256f45e-a308-4d26-9766-77d92425a97f
52350302_tran_6697_op.gmlの
tran_6a3300c9-3236-45eb-b2b6-802be8bfda3b
tran_c144d4b1-d15c-47c7-8e5b-6cbc92f84f3f
がうまく読めない


CreateRoadLOD3
	cgp.GetTRANLOD3s	cordataTRANLOD3.coroutine1
	makeTRANLOD3Model	cordataTRANLOD3.coroutine2




CreateDemAll8			cordataDEM.coroutine3
	makeDEM8		    cordataDEM.coroutine1 
			GenerateMesh    cordataDEM.coroutine2

CreateDemAll6			cordataDEM.coroutine3
	makeDEM6		    cordataDEM.coroutine1 
			GenerateMesh    cordataDEM.coroutine2



CreateRoad8			cordataRoad8.coroutine1 
	List<Relief> readTRANRelief
		List<Relief> readRoadReliefFromReader
	makeTran		cordataTRAN.coroutine1

CreateRoad6			cordataRoad6.coroutine1
	List<Relief> readTRANRelief
		List<Relief> readRoadReliefFromReader
	makeTran		cordataTRAN.coroutine1



CreateBLDGLOD3			cordataBLDGLOD3.coroutine3
	makeBLDGLOD3
		cgp.GetBuildingsLOD3	cordataBLDGLOD3.coroutine1
		makeBLDGModel		cordataBLDGLOD3.coroutine2

CreateBLDGLOD2			cordataBLDG.coroutine3
	makeBLDGLOD2
		cgp.GetBuildings	cordataBLDG.coroutine1
		makeBLDGModel		cordataBLDG.coroutine2



CreateFRN			cordataFRN.coroutine3
	makeFRN
		cgp.GetFRNs		cordataFRN.coroutine1
		makeFRNModel		cordataFRN.coroutine2


CreateVEG			cordataVEG.coroutine3
	makeVEG
		cgp.GetVEGs		cordataVEG.coroutine1
		makeVEGModel		cordataVEG.coroutine2


●地面道路生成・配列使用　

1 仮地面生成
2 高さ配列
3 仮地面削除

4 仮道路生成（高さ）
5 道路配列
6 仮道路削除

7 平均化
8 道路生成（平均）
9 地面生成

●地面道路生成・配列未使用　

1 仮地面生成
2 道路生成（高さ）
3 仮地面削除

4 地面生成


●地面生成
地面生成



●道路生成
道路生成（高さ）

*/






/* 地域メッシュコードを扱うクラス
緯度経度と3次メッシュコード(4桁2桁2桁)の相互変換用
add3メソッドで3次メッシュコードでオフセットした位置のメッシュコードを取得
add2メソッドで2次メッシュコードでオフセットした位置のメッシュコードを取得

https://nlftp.mlit.go.jp/ksj/old/old_data_mesh.html





*/
class GridSquareMeshCode 
{
    public string index;///3次メッシュ 8桁
    public string index2;//2次メッシュ 6桁
    public int p; //z
    public int u; //x
    public int q; //z
    public int v; //x
    public int r; //z
    public int w; //x
    public double lat;
    public double lon;

    // メッシュコードから緯度経度を生成
    public GridSquareMeshCode(string mapindex) {
        this.index = mapindex;
        this.index2 = mapindex.Substring(0,6);
        //Debug.Log(mapindex.Length);

        if (mapindex.Length >= 4) {
            p = Convert.ToInt32(mapindex.Substring(0,2));
            u = Convert.ToInt32(mapindex.Substring(2,2));
            
            lat = p/1.5;
            lon = u+100.0;
        }
        if (mapindex.Length >= 6) {
            q = Convert.ToInt32(mapindex.Substring(4,1));
            v = Convert.ToInt32(mapindex.Substring(5,1));
            
            lat += ((q *5.0 /100)/60*100);
            lon += (((v*7.5)/100)/60*100);
        }
        if (mapindex.Length >= 8 ) {
            r = Convert.ToInt32(mapindex.Substring(6,1));
            w = Convert.ToInt32(mapindex.Substring(7,1));

            lat += (((r*30.0)/100000)/(60*60)*100000);
            lon += (((w*45.0)/100000)/(60*60)*100000);
        }
    }

    // 緯度経度からメッシュコードを生成
    public GridSquareMeshCode(double lat, double lon) 
    {
        // this.lat = lat;
        // this.lon = lon;

        p = (int)(lat*60/40);
        double a = lat*60-p*40;
        u = (int)(lon-100);
        double f = lon - (u+100);

//        Debug.Log(p+" "+u+"    "+a+" "+f);

        q = (int)(a/ 5);
        double b = a - q*5;

        v = (int)(f*60/7.5);
        double g = f*60 - v*7.5;

//       Debug.Log(q+" "+v+"      "+b+" "+g);

        r = (int)(b*60/30);
        w = (int)(g*60/45);

//       Debug.Log(r+" "+w);

        index = ""+p+u+q+v+r+w;
        index2 = index.Substring(0,6);
        GridSquareMeshCode gs = new GridSquareMeshCode(index);
        this.lat = gs.lat;
        this.lon = gs.lon;

    }

    // 度の値で追加
    public GridSquareMeshCode add(double addlat, double addlon)
    {
        return new GridSquareMeshCode(lat+addlat,lon+addlon);
    }

    // ３次メッシュの値で追加
    public GridSquareMeshCode add3(int z, int x)
    {
        // Debug.Log("index" +index+" "+lat+" "+lon);
        double newlat = lat + (z+0.5) * 30.0/3600   ;
        double newlon = lon + (x+0.5) * 45.0/3600;
        GridSquareMeshCode newg = new  GridSquareMeshCode(newlat,newlon);
        // Debug.Log("newg.index" +newg.index);
        return newg;
    }

    // ２次メッシュの値で追加
    public GridSquareMeshCode add2(int z, int x)
    {
        // Debug.Log("index "+index);
        double newlat = lat + z * 5.0/60 + 0.5 * 30.0/3600 ;
        double newlon = lon + x * 7.5/60 + 0.5 * 45.0/3600;
        GridSquareMeshCode newg = new  GridSquareMeshCode(newlat,newlon);
//         Debug.Log("newg.index "+newg.index);
        return newg;
    }

    // ２次メッシュでのブロックの差を返す
    public (int z, int x) diff2(GridSquareMeshCode l) {
        double latdiff = lat - l.lat;
        double londiff = lon - l.lon;
        double cz = latdiff / (5.0/60);
        double cx = londiff / (7.5/60);
        int iz = (int)Math.Round(cz);
        int ix = (int)Math.Round(cx);
        Debug.Log(iz+" "+ix);
        return (iz,ix);
        // return ((q - l.q),(v- l.v));
    }
    // 3次メッシュでのブロックの差を返す
    public (int z, int x) diff3(GridSquareMeshCode l) {
        double latdiff = lat - l.lat;
        double londiff = lon - l.lon;
        double cz = latdiff / (30.0/3600);
        double cx = londiff / (45.0/3600);
        int iz = (int)Math.Round(cz);
        int ix = (int)Math.Round(cx);
        Debug.Log(iz+" "+ix);
        return (iz,ix);
        // return ((q - l.q),(v- l.v));
    }

}
class TexRef {
    public Texture2D tex;
}
/*
　マップタイルを扱うクラス
緯度経度とマップタイル(z,x,y)の相互変換用

https://maps.gsi.go.jp/development/tileCoordGetDistSum.html
https://maps.gsi.go.jp/development/ichiran.html
以下のz,x,yの計算用
https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg
*/
class MapTile 
{
    public int x;
    public int y;
    public int z;
    public double lat;
    public double lon;
    public double test;

    /// 緯度経度からマップタイルの番号を計算
    public MapTile(double lat,double lon, int z){
        this.z = z;
        this.lat = lat;
        this.lon = lon;
        x = (int)(((lon/180+1)*Math.Pow(2,z)/2));
        y = (int)(((Math.PI-Math.Log(Math.Tan((45 + lat/2)*Math.PI/180)) )*Math.Pow(2,z))/(2*Math.PI));
    }

    /// マップタイルの番号から緯度経度を計算
    public MapTile(int x, int y, int z) 
    {
        this.z = z;
        this.x = x;
        this.y = y;
        lon = (x / Math.Pow(2,z))*360-180;
        double mapy = (y / Math.Pow(2,z))*2*Math.PI - Math.PI;
        lat = 2* Math.Atan(Math.Pow(Math.E,-mapy))*180/Math.PI - 90;
    }
}



/*
　PlateauCityGMLのModelGeneratorをベースにUnity上でMesh等を生成

*/
class UnityModelGenerator :ScriptableObject
 {
    public bool useCollider;
    public bool useTexture;
    public Material bldgMaterial;
    public bool saveMeshAsAsset = false; // プレハブ化やパッケージ化するときはtrue
        
    int texsize;
    public Triangle[] Triangles { get; private set; }
    public Vector2[] UV { get; private set; }
    public Vertex[] Vertices { get; private set; }
    public string TextureFile { get; private set; }

    public Position LowerCorner { get; private set; }
    public Position UpperCorner { get; private set; }
    public Position Origin { get; private set; }

    private List<string> model = new List<string>();
//        private Building _building;
    public Building _building;
    public bool useColor;
    public string [] bldgcolors;
    bool bldgMaxColor;
     //   bool setUV = false;
    string prevTextureFile;

    Texture2D tex;// = Resources.Load(loaddirPath+@"\"+fileNameWithoutExtension, typeof(Texture2D)) as Texture2D;
    Material material;// = Resources.Load(loaddirPath+@"\"+fileNameWithoutExtension, typeof(Material)) as Material;  

    public GameObject prefab;
    public float height;
    public int vegTreePrefabNumber;

    public void ModelInitialize(Building building, Position origin, bool useCollider, bool useTexture, Material bldgMaterial, int texsize, bool saveMeshAsAsset)
    {
        ModelInitialize(building, origin);
        this.useCollider = useCollider;
        this.useTexture = useTexture;
        this.bldgMaterial = bldgMaterial;
        this.texsize = texsize;
        this.saveMeshAsAsset = saveMeshAsAsset;
        this.useColor = false;
    }



    public void ModelInitialize(Building building, Position origin, bool useCollider, bool useTexture, Material bldgMaterial, int texsize, bool saveMeshAsAsset, bool useColor,  string [] bldgcolors,bool bldgMaxColor)
    {
        ModelInitialize(building, origin,useCollider,useTexture,bldgMaterial,texsize, saveMeshAsAsset);
        this.useColor = useColor;
        this.bldgcolors = bldgcolors;
        this.bldgMaxColor = bldgMaxColor;
    }

//        private void ModelInitialize(Building building, Position origin)
        public void ModelInitialize(Building building, Position origin)
        {
            _building = building;
            List<Triangle> tris = new List<Triangle>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vertex> vtx = new List<Vertex>();
            LowerCorner = building.LowerCorner;
            UpperCorner = building.UpperCorner;

            Origin = origin;
            int count = 0;
            int offset = 0;
            string textureFile = null;
            TextureFile = null;
            Surface[] surfaces = building.LOD2Solid;
            if(surfaces == null)
            {
                surfaces = building.LOD1Solid;
            }
            for (int i = 0; i < surfaces.Length; i++, count++)
            {
                if (surfaces[i].Positions != null)
                {
                    Triangulator tr = new Triangulator();
                    if (surfaces[i].Positions.Count() <3) {
                        continue;
                    }
                    (Vertex[] vertex, Triangle[] triangle) = tr.Convert(surfaces[i].Positions, surfaces[i].UVs, offset, Origin);
                    vtx.AddRange(vertex);
                    tris.AddRange(triangle);
                    offset += vertex.Length;
                    if (surfaces[i].UVs != null)
                    {
                        uvs.AddRange(surfaces[i].UVs);
                    }
                    else
                    {
                        if (true) {
                            Vector3 side1 = vertex[0].Value - vertex[1].Value;
                            Vector3 side2 = vertex[2].Value - vertex[1].Value;
                            Vector3 cross = Vector3.Cross(side1,side2);

                            GameObject  go = new GameObject("Tmp");
                            go.transform.position = vertex[1].Value;
                            go.transform.forward = cross.normalized;

                            for (int j = 0; j < vertex.Length; j++)
                            {
                                // int uvsize = 1000;
                                Vector3 v3 = new Vector3(0.5f,0.5f,0.5f) + go.transform.InverseTransformPoint(vertex[j].Value)/100;
                                Vector2 v2 = new Vector2(v3.x,v3.y);
//                                Debug.Log(" vertex "+  vertex[j].Value+" v3 "+v3+" v2 "+v2);
                                uvs.Add(v2);
                            }
                            DestroyImmediate(go);
                        }
                    }
                    if (surfaces[i].TextureFile != null)
                    {
                        textureFile = surfaces[i].TextureFile;
                    }
                }
            }
            Triangles = tris.ToArray();
            Vertices = vtx.ToArray();
            UV = uvs.ToArray();
            string current = Path.GetDirectoryName(building.GmlPath);
            if (textureFile != null)
            {
                TextureFile = Path.Combine(current, textureFile);
            }
        }

    private Texture2D GetResized(Texture2D texture, int width, int height)
    {
        Debug.Log("resize");
        RenderTexture prevActiveRT = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        RenderTexture.active = rt;

        Graphics.Blit(texture, rt);
        Texture2D resizedt2d = new Texture2D(width, height);
        resizedt2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        resizedt2d.Apply();

        RenderTexture.active = prevActiveRT;
        RenderTexture.ReleaseTemporary(rt);
        return resizedt2d;
    }

    public void Create(GameObject bldg, bool useAVGPosition,string currentMapIndex,int number){
        Debug.Log("UnityModelGenerator "+_building.Id+" "+_building.Name+" Vertices.Length:"+Vertices.Length);
        if (Vertices.Length == 0 ) 
        {
            return;
        }
        if (_building.Id == "") 
        {
            //return;
            _building.Id = "bldg"+number;
        }        
        // if (_building.Id != "bldg_db06c11a-8e38-450c-8fd1-a78492933a44") 
        // {
        //     return;
        // }
        GameObject bldgX = new GameObject(_building.Id);
        var vertices = new Vector3[Vertices.Length];
        // GameObject go1   = GameObject.CreatePrimitive (PrimitiveType.Sphere);
        //Debug.Log("vertices.Length "+vertices.Length);
        for(int i = 0; i < vertices.Length; i++) {
            vertices[i] = Vertices[i].Value;
            // Debug.Log(vertices[i]);
            // GameObject go = Instantiate(go1, vertices[i], Quaternion.identity);
            // go.name = "Pos" + i;

        }

        // x z の平均とyの最小値を求める　transformを実際の場所にするため
        Vector3 vbase = new Vector3();
        if(useAVGPosition) {
            float ymax = 0;//vertices[0].y;
            vbase.y = float.MaxValue;
            for(int i = 0; i < vertices.Length; i++) {
                vbase.x += vertices[i].x;
                vbase.z += vertices[i].z;
                vbase.y = UnityEngine.Mathf.Min(vbase.y, vertices[i].y);
                ymax = UnityEngine.Mathf.Max(ymax ,vertices[i].y);
            }
            vbase.x /= vertices.Length;
            vbase.z /= vertices.Length;
            for(int i = 0; i < vertices.Length; i++) {
                vertices[i].x -= vbase.x;            
                vertices[i].z -= vbase.z;            
                vertices[i].y -= vbase.y;            
            }

            // すでに同じ名前の物体があれば生成しない。
            Ray ray = new Ray(new Vector3(vbase.x,ymax,vbase.z), -Vector3.up);
            RaycastHit hit = new RaycastHit();
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.gameObject.name == _building.Id) {
                    Debug.Log("hit.collider.gameObject.name "+hit.collider.gameObject.name+" "+_building.Id);
                    return;
                }
            }            

        }

        //var uv = new Vector2[count * 3];
        var triangles = new int[Triangles.Length*3];
        for(int i = 0; i < Triangles.Length; i++) {
            triangles[i*3+0] = Triangles[i].P0.Index;
            triangles[i*3+1] = Triangles[i].P2.Index;
            triangles[i*3+2] = Triangles[i].P1.Index;
        }
        // for(int i = 0; i < UV.Length; i++) {
        //     Debug.Log(UV[i]);
        // }

        bldgX.transform.parent = bldg.transform;
        bldgX.AddComponent<MeshFilter>();
        bldgX.AddComponent<MeshRenderer>();
        if(useAVGPosition) {
            bldgX.transform.position = vbase;
        }
        if (_building.modelType == "veg:SolitaryVegetationObject") {
                    Debug.Log("Height "+_building.Height+" "+prefab);
                    if (prefab != null){
                        if (_building.modelNumber == vegTreePrefabNumber) {
                            //Debug.Log("random "+UnityEngine.Random.Range(0,360));
                            var obj = Instantiate(prefab, vbase,Quaternion.Euler(0,UnityEngine.Random.Range(0,360),0));  
                            obj.transform.parent = bldgX.transform;
                            obj.transform.localScale = Vector3.one * _building.Height / height;
                        }
                        return;
                    }         
        }
        Mesh mesh = new Mesh();
        mesh.RecalculateNormals();
        var filter = bldgX.GetComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = UV;
        mesh.RecalculateNormals();
        if (useCollider && ! _building.nocollider) 
        {
            bldgX.AddComponent<MeshCollider>();    
            var collider = bldgX.GetComponent<MeshCollider>();
            collider.sharedMesh  = mesh;
        }

        // プレハブ化するためにアセットを保存する
        if (saveMeshAsAsset){
            string saveMeshdirPath = @"Assets\Resources\Mesh\"+currentMapIndex;
            if (!Directory.Exists(saveMeshdirPath)) Directory.CreateDirectory(saveMeshdirPath);
            string saveMeshFilename = saveMeshdirPath+@"\"+_building.Id+".asset";

    //        var asset = AssetDatabase.LoadAssetAtPath(path,type);
            Mesh asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(saveMeshFilename);//,typeof(Mesh));
            if( asset == null){
    //            AssetDatabase.CreateAsset(newAsset, path);
                UnityEditor.AssetDatabase.CreateAsset(filter.sharedMesh,saveMeshFilename );
            } else {
                filter.sharedMesh = asset;
            }
        }
     //Debug.Log("(TextureFile "+TextureFile);
     //Debug.Log(" ext" + Path.GetExtension(TextureFile));
    //    Debug.Log("TextureFile "+ TextureFile +" "+useTexture +" "+ useColor) ;
     
        if (TextureFile != null && (useTexture || useColor) )
        {
            //Debug.Log("TextureFile "+TextureFile+","+prevTextureFile);
            // 都市設備が同じテクスチャを使う場合が多いので
            // 前回と同じファイルならそのまま使う
            if (TextureFile == prevTextureFile) {
                //Debug.Log("SameTextureFile ");
                var renderer = bldgX.GetComponent<Renderer>();
                material.mainTexture = tex;//
                renderer.sharedMaterial = material;

            } else {
                prevTextureFile = TextureFile;

                string fileNameWithoutExtension  = Path.GetFileNameWithoutExtension(TextureFile);
                //Debug.Log(" GetFileNameWithoutExtension " + fileNameWithoutExtension);
//                string loaddirPath = Path.GetDirectoryName(TextureFile.Substring(TextureFile.IndexOf(@"udx\")));
                string loaddirPath = "Texture"+Path.GetDirectoryName(TextureFile.Substring(TextureFile.IndexOf(@"udx\")+3));
                string savedirPath = @"Assets\Resources\"+loaddirPath;
                //Debug.Log("loaddirPath " + loaddirPath);
                //Debug.Log("savedirPath " + savedirPath);
                if (!Directory.Exists(savedirPath))
                { 
                    //Debug.Log("!exists");
                    Directory.CreateDirectory(savedirPath);
                } else {
                    //Debug.Log("exists");
                }
                // Texture2D tex = Resources.Load(loaddirPath+@"\"+fileNameWithoutExtension, typeof(Texture2D)) as Texture2D;
                // Material material = Resources.Load(loaddirPath+@"\"+fileNameWithoutExtension, typeof(Material)) as Material;  
                
                tex = Resources.Load(loaddirPath+@"\"+fileNameWithoutExtension, typeof(Texture2D)) as Texture2D;
                material = Resources.Load(loaddirPath+@"\"+fileNameWithoutExtension, typeof(Material)) as Material;  
                string savefilename =  savedirPath+@"\"+fileNameWithoutExtension+".asset";
                //if (tex == null || material == null) {
                if (tex == null ) { // 新規のテクスチャの場合
                    //Debug.Log("Create Material "+loaddirPath);
                    tex = new Texture2D(1, 1);
                    using( FileStream fs = new FileStream(TextureFile, FileMode.Open)){
                        using( BinaryReader bin = new BinaryReader(fs) ) {
                            byte[] result = bin.ReadBytes((int)bin.BaseStream.Length);
                        //bin.Close();
                            tex.LoadImage(result);
                            if (tex.width > texsize || tex.height >texsize) {
                                int texwidth = tex.width;
                                int texheight = tex.height;
                                // var resizedTexture = new Texture2D(tex.width/4, tex.height/4);
                                // Graphics.ConvertTexture(tex,resizedTexture);
                                if (tex.width > texsize) texwidth = texsize;
                                if (tex.height > texsize) texheight = texsize;
                                tex = GetResized(tex, texwidth, texheight);
                            }
                            var assetT = UnityEditor.AssetDatabase.LoadAssetAtPath(savefilename,typeof(Texture));
                            if( assetT == null){
                                UnityEditor.AssetDatabase.CreateAsset( tex, savefilename);
                            }    
                        }
                    }
                }
                var renderer = bldgX.GetComponent<Renderer>();
                Material materialC = null;
                if (useColor) {
                    // LOD2のテクスチャが粗い場合用にテクスチャから単色でMaterialを生成
                    Color color = GetColorFromTexture(tex,bldgMaxColor);
                    _building.color = SimpleColor(color);
                    materialC = CreateColorMaterial(_building.color,bldgX,"ColorBLDG");  
                }
                if (useTexture)  {
                    material = new Material(Shader.Find("Unlit/Texture"));
                    material.mainTexture = tex;//
                    //         var tempMaterial = new Material(renderer.sharedMaterial);
                    //material.mainTexture = tex;
                    renderer.sharedMaterial = material;
                } else if (useColor) {
                    renderer.sharedMaterial = materialC;
                }
                //UnityEditor.AssetDatabase.CreateAsset( material,  );
                savefilename =  savedirPath+@"\"+fileNameWithoutExtension+".mat" ;
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath(savefilename,typeof(Material));
                if( asset == null){
                    UnityEditor.AssetDatabase.CreateAsset( material, savefilename);
                }  
            }
        } else { // テクスチャがない場合
            if (useColor) { // 色を付ける場合は フォルダの中にある色からランダムで選ぶ
                string strcolor = bldgcolors[UnityEngine.Random.Range(0, bldgcolors.Length)];
                Color color = GetColor(strcolor);
                _building.color = color;//SimpleColor(color);    
                Material material = CreateColorMaterial(_building.color,bldgX,"ColorBLDG");                   
                var renderer = bldgX.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                
            } else {    // 色を付けない場合
            Debug.Log("_building.modelType"+_building.modelType);
                if (_building.modelType == "veg:PlantCover") {
                    if (bldgMaterial != null) {
                        MeshRenderer meshRenderer = bldgX.GetComponent<MeshRenderer>();
                        meshRenderer.sharedMaterial = bldgMaterial;      
                    } else {
                        Material material = CreateColorMaterial(_building.color,bldgX,"Color");
                        var renderer = bldgX.GetComponent<Renderer>();
                        renderer.sharedMaterial = material;
                    }               
                } else if (_building.modelType == "veg:SolitaryVegetationObject") {
                    Debug.Log("Height "+_building.Height+" "+prefab);
                    if (prefab != null) {
                        var obj = Instantiate(prefab);  
                        obj.transform.parent = bldgX.transform;
                        obj.transform.position = new Vector3();
                    } else {
                        Material material = CreateColorMaterial(_building.color,bldgX,"Color");
                        var renderer = bldgX.GetComponent<Renderer>();
                        renderer.sharedMaterial = material;
                    }
                // } else if (bldgMaterial != null) { // マテリアルが指定されていればそのマテリアル
                //     MeshRenderer meshRenderer = bldgX.GetComponent<MeshRenderer>();
                //     meshRenderer.sharedMaterial = bldgMaterial;
                                     
                } else {  // マテリアルがなければ色                  //Debug.Log("Color "+_building.color);
                    Material material = CreateColorMaterial(_building.color,bldgX,"Color");
                    var renderer = bldgX.GetComponent<Renderer>();
                    renderer.sharedMaterial = material;

                //                bldgX.GetComponent<Renderer>().material.color = _building.color;
                }
            }
            // dem.GetComponent<Renderer>().sharedMaterial.mainTexture = tex;
//            bldg.color
        }
        //Resources.UnloadUnusedAssets();
    }

    // テクスチャから色を選ぶ
    Color GetColorFromTexture(Texture2D tex, bool bldgMaxColor){
        float cr = 0;
        float cg = 0;
        float cb = 0;
        float max = 0;
        int count = 0;
        // for(int ix = 0; ix < tex.width; ix++)
        // for(int iy = 0; iy < tex.height; iy++)
        
        //  {
        //     var tmpcolor = tex.GetPixel(ix,iy);
        for(int i = 0; i < 100; i++) {
            var tmpcolor = tex.GetPixel(UnityEngine.Random.Range(0,tex.width), UnityEngine.Random.Range(0,tex.height));
            if (bldgMaxColor) { // 最大の値の色を選ぶ
                if (tmpcolor.r + tmpcolor.g + tmpcolor.b > max) {
                    max = tmpcolor.r + tmpcolor.g + tmpcolor.b;
                    cr = tmpcolor.r; 
                    cg = tmpcolor.g; 
                    cb = tmpcolor.b; 
                    count = 1;                                            
                }
            } else {   // 平均値で色を決める
                if (tmpcolor.r > 0.4 || tmpcolor.g > 0.4 || tmpcolor.b > 0.4) {
                    max = tmpcolor.r + tmpcolor.g + tmpcolor.b;
                    cr += tmpcolor.r; 
                    cg += tmpcolor.g; 
                    cb += tmpcolor.b; 
                    count++;
                
                }                                        
            }
        }
        Color color = new Color(cr/count,cg/count,cb/count,1);
        return color;
    }

    // 16進数文字列から色を決める RRGGBB 
    Color GetColor(string s) {
        int cr = Convert.ToInt32(s.Substring(0,2),16);
        int cg = Convert.ToInt32(s.Substring(2,2),16);
        int cb = Convert.ToInt32(s.Substring(4,2),16);
        return new Color(cr/255f,cg/255f,cb/255f,1);

    }

    // 色数を減らすためにFFのうち 2桁目のみにする
    Color SimpleColor(Color c) {
        int cr = (int)(c.r * 16);
        int cg = (int)(c.g * 16);
        int cb = (int)(c.b * 16);
        return new Color(cr/16f,cg/16f,cb/16f,1);
    }

    // 色からマテリアルを作る
    Material CreateColorMaterial(Color c, GameObject bldgX, string path){
        int cr = (int)(c.r * 255);
        int cg = (int)(c.g * 255);
        int cb = (int)(c.b * 255);
        string colorID = cr.ToString("X2")+cg.ToString("X2")+cb.ToString("X2");
        //Debug.Log("COLOR "+colorID);

        string fileNameWithoutExtension  = colorID;
        //Debug.Log(" GetFileNameWithoutExtension " + fileNameWithoutExtension);
        string loaddirPath =  path;
        string savedirPath = @"Assets\Resources\"+loaddirPath;
        //Debug.Log("loaddirPath " + loaddirPath);
        //Debug.Log("savedirPath " + savedirPath);
        if (!Directory.Exists(savedirPath))
        {
            //Debug.Log("!exists");
            Directory.CreateDirectory(savedirPath);
        } else {
            //Debug.Log("exists");
        }
        Material material = Resources.Load(loaddirPath+@"\"+fileNameWithoutExtension, typeof(Material)) as Material;  
        if (material == null) {
            //Debug.Log("Create Material "+loaddirPath);
            // var renderer = bldgX.GetComponent<Renderer>();
            material = new Material(Shader.Find("Standard"));
            material.color = c;
            // renderer.sharedMaterial = material;
            //UnityEditor.AssetDatabase.CreateAsset( material,  savedirPath+@"\"+fileNameWithoutExtension+".mat" );
            string savefilename =  savedirPath+@"\"+fileNameWithoutExtension+".mat" ;
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath(savefilename,typeof(Material));
            if( asset == null){
                UnityEditor.AssetDatabase.CreateAsset( material, savefilename);
            }              
            material = Resources.Load(loaddirPath+@"\"+fileNameWithoutExtension, typeof(Material)) as Material;  
        } else {
            //Debug.Log("Load Material "+loaddirPath);
            // var renderer = bldgX.GetComponent<Renderer>();
            // renderer.sharedMaterial = material;
        }         
        return material;
    }

}























public class DemBldgTranCreator : MonoBehaviour
{


    // Inspectorに表示される部分
    /* udpフォルダの場所　 */
    public string udxpath = @"C:\PLATEAU\40205_iizuka-shi_2020_citygml_5_op\40205_iizuka-shi_2020_citygml_x_op\udx\";//dem\50303564_dem_6697_op.gml";
    public string basemapindex = "50303564";     // メッシュの番号　８桁の数字の文字列　(0,0,0)の基準位置
    public string zeromapindex = "50303564";     // メッシュの番号　８桁の数字の文字列　左下（南西）の位置
    public int xsize = 1;               // x（経度方向）に何ブロック生成するか
    public int zsize = 1;               // z（経度方向）に何ブロック生成するか
    public bool saveMeshAsAsset = false; // プレハブ化やパッケージ化するときはtrue　かなり時間がかかるのでfalseでいろいろ試して最後にtrueに
    public bool useAVGPosition = true; // 建物の位置をMeshの平均（yはMin）に
    
    [Space( 16)]
    public bool roadON = false;          // 道路を生成するならtrue
    public bool roadLOD3 = false;          // LOD3の道路を生成するならtrue
    public bool roadLOD1 = false;          // LOD1の道路を生成するならtrue
    public float rayHeight = 1000;
    public bool roadLOD1Array = false;          // 地面の高さの配列で高さを平均化するならtrue meshcodeが8桁の道路のみ　6桁は地面の高さから直接
    public float roadLOD1SplitLength = 0;    // 長い1辺のときの分割する長さ(m) 5
    public float roadLOD1MargeLength = 0.5f; // 近い点をまとめる長さ(m) // 別の面との同一点はみてないのでギャップがおこるかも。
    public bool roadLOD1SlowButGood = false; // よりよい分割（とりあえず長さが最小
    public bool roadUseCollider = false; // Colliderをアタッチするならtrue    
    public Material roadMaterial;       // 道のMaterial


    [Space( 16)]

    public bool demON = true;           // 地形を生成するならtrue
    public bool demUseCollider = true;   // 地形にColliderをアタッチするならtrue
    public bool demUseTexture = true;   // 地形に地理院地図の画像を貼るならtrue
    public bool demAdjustRoad = true;   // 地形を道路等の高さに合わせるて下げるならtrue    
    public Material demMaterial;        // 画像を貼らない場合のMaterial

    [Space( 16)]

    public bool bldgON = false;          // 建物を生成するならtrue
    public bool bldgLOD3 = false;          // LOD3の建物を生成するならtrue
    public bool bldgLOD2 = false;          // LOD2の建物を生成するならtrue
    public bool bldgUseCollider = false; // 建物にColliderをアタッチするならtrue
    public bool bldgLOD3UseTexture = false;  // 建物に画像を貼るならtrue
    public bool bldgLOD2UseTexture = false;  // 建物に画像を貼るならtrue
    public bool bldgLOD3UseColor = false;  // 建物を単色の色を付けるならtrue    
    public bool bldgLOD2UseColor = false;  // 建物を単色の色を付けるならtrue    
    public bool bldgMaxColor = false;  //  Textureから明るい色を取得するならtrueそうでなければ平均    
    public int bldgTexsize = 256;       // テクスチャのサイズ
    public Material bldgMaterial;       // 画像を貼らない場合のMaterial

    [Space( 16)]

    public bool frnON = true;          // 都市設備を生成するならtrue
    public bool frnUseCollider = false; // 都市設備にColliderをアタッチするならtrue
    public bool frnUseTexture = true;  // 都市設備に画像を貼るならtrue
    public int  frnTexsize = 256;       // テクスチャのサイズ
    public float frnOffsetY = 0.009f;   // 道路と重なりを避けるために上に少しあげる
    public bool frnSplit = false;        // 分割するならtrue

    [Space( 16)]

    public bool vegON = true;          // 植生を生成するならtrue
    public bool vegUseCollider = false; // 植生にColliderをアタッチするならtrue
    //private bool vegUseTexture = false;  // 植生に画像を貼るならtrue
    //public Material vegMaterial;       // 画像を貼らない場合のMaterial
        public Material vegPlantCoverMaterial;       // 画像を貼る場合のMaterial tileは100に
    public GameObject vegTreePrefab;
    public int vegTreePrefabNumber = 1; // 大阪は0 沼津は1 幹と葉の幹に合わせる
    public float vegTreePrefabHeight;

    [Space( 16)]

    public string currentcreating = "";

    // 基準位置
    Position baseLowerCorner;

    // 生成範囲　
    GridSquareMeshCode mizero; //左下 3次8桁
    GridSquareMeshCode mimax;　//右上(範囲外）) 3次8桁
        Position pzero;
        Position pmax; 
        Vector3 v3zero;
        Vector3 v3max;

    int heightarraysize = 100000;//1000;
    int avgsize = 5;
    float[,] heightArray;// = new float[100, 100];
    float[,] heightSumArray;
    int[,] heightCountArray;
    int[,] roadArray;
    float[,] heightAverageArray;        


    UnityModelGenerator mg;


    /*    
    Triangulator - Unify Community Wiki がベース
    http://wiki.unity3d.com/index.php?title=Triangulator
    */
    // 三角形ポリゴンに分割
    public int[] Triangulate(Vector2[] points)
    {
        List<Vector2> pointList = new List<Vector2>(points);
        List<int> indices = new List<int>();

        int n = pointList.Count;
        if (n < 3)
            return indices.ToArray();

        int[] V = new int[n];
        if (Area(pointList) > 0)
        {
            for (int i = 0; i < n; i++)
                V[i] = i;
        }
        else
        {
            for (int i = 0; i < n; i++)
                V[i] = (n - 1) - i;
        }



        //int count = 2 * n;
        int v = n - 1;  // vは最初最後の点
        for (int nv = n; nv > 2; nv--)
        {
            string tmp = "";
            for (int j = 0; j < nv; j++){
                tmp += V[j]+" ";
            }
            // 
            // if ((count--) <= 0){
            //     Debug.Log("Break #######################################################################");
            //     break;
            // }

            int target = v;
            float min = float.MaxValue;
            for(int i = 0; i < nv; i++) {
                int pt0 = (v+i)%nv;
                int pt1 = (v+1+i)%nv;
                int pt2 = (v+2+i)%nv;
//                Debug.Log(" i:"+i+" "+IsEar(pt0, pt1, pt2, nv, V), m_points+" "+GetDistSum(pt0,pt1,pt2,n,V,m_points ));
                // 長さが一番大きな三角形を優先して切り出す。このあたりは方法を要検討！
                if (IsEar(pt0, pt1, pt2, nv, V, pointList) && GetDistSum(pt0,pt1,pt2,nv,V,pointList) < min) {
                    min = GetDistSum(pt0,pt1,pt2,nv, V, pointList);
                    target = v+i;
                    if (!roadLOD1SlowButGood) {
                        break;
                    }
                    // if (min< 50) {
                    //     break;
                    // }
                }
            }
            v = target;
            indices.Add(V[(v)%nv]);
            indices.Add(V[(v+1)%nv]);
            indices.Add(V[(v+2)%nv]);
            v = (v+1)%nv;
            int s, t;
            for (s = v, t = v + 1; t < nv; s++, t++)
                V[s] = V[t];
            // count = 2 * nv;
        }

        indices.Reverse();
        return indices.ToArray();
    }
    private float GetDistSum(int u, int v, int w, int n, int []V, List<Vector2> pointList) {
        Vector2 A = pointList[V[u]];
        Vector2 B = pointList[V[v]];
        Vector2 C = pointList[V[w]];        
        return Vector2.Distance(A,B)+Vector2.Distance(B,C)+Vector2.Distance(C,A);
    }
    private float Area( List<Vector2> pointList)
    {
        int n = pointList.Count;
        float A = 0.0f;
        for (int p = n - 1, q = 0; q < n; p = q++)
        {
            Vector2 pval = pointList[p];
            Vector2 qval = pointList[q];
            A += pval.x * qval.y - qval.x * pval.y;
        }
        return (A * 0.5f);
    }

    private bool IsEar(int u, int v, int w, int n, int[] V, List<Vector2> pointList)
    {
        int p;
        Vector2 A = pointList[V[u]];
        Vector2 B = pointList[V[v]];
        Vector2 C = pointList[V[w]];
        if (Mathf.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x))))
            return false;
        for (p = 0; p < n; p++)
        {
            if ((p == u) || (p == v) || (p == w))
                continue;
            Vector2 P = pointList[V[p]];
            if (InsideTriangle(A, B, C, P))
                return false;
        }
        return true;
    }

    private bool InsideTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax, ay, bx, by, cx, cy, apx, apy, bpx, bpy, cpx, cpy;
        float cCROSSap, bCROSScp, aCROSSbp;

        ax = C.x - B.x; ay = C.y - B.y;
        bx = A.x - C.x; by = A.y - C.y;
        cx = B.x - A.x; cy = B.y - A.y;
        apx = P.x - A.x; apy = P.y - A.y;
        bpx = P.x - B.x; bpy = P.y - B.y;
        cpx = P.x - C.x; cpy = P.y - C.y;

        aCROSSbp = ax * bpy - ay * bpx;
        cCROSSap = cx * apy - cy * apx;
        bCROSScp = bx * cpy - by * cpx;

        return ((aCROSSbp >= 0.0f) && (bCROSScp >= 0.0f) && (cCROSSap >= 0.0f));
    }
























    // "緯度 経度 高度"の文字列をPositionクラスのオブジェクトに
    public static Position getPosition(string positionString)
    {
        string[] items = positionString.Split(' ');
        if (items.Length ==3) {
           return new Position
            {
                Latitude = Convert.ToDouble(items[0]),
                Longitude = Convert.ToDouble(items[1]),
                Altitude = Convert.ToDouble(items[2])
            };
        } else
        {
            return new Position();
        }
    }
    // lowerCornerとupperCornerを取得
    (Position lowerCorner, Position upperCorner) getCornerFromIndex2(string currentMapIndex){

        GridSquareMeshCode miL = new GridSquareMeshCode(currentMapIndex);
        GridSquareMeshCode miU = miL.add2(1,1);
        Position lowerCorner = new Position(miL.lat, miL.lon,0);
        Position upperCorner = new Position(miU.lat, miU.lon,0);
        return (lowerCorner, upperCorner);

    }

    // lowerCornerとupperCornerを取得
    (Position lowerCorner, Position upperCorner) getCornerFromIndex3(string currentMapIndex)
    {
        GridSquareMeshCode miL = new GridSquareMeshCode(currentMapIndex);
        GridSquareMeshCode miU = miL.add3(1,1);
        Position lowerCorner = new Position(miL.lat, miL.lon,0);
        Position upperCorner = new Position(miU.lat, miU.lon,0);
        return (lowerCorner, upperCorner);

    }
    // lowerCornerとupperCornerを取得
    (Position lowerCorner, Position upperCorner) getCornerFromReader(XmlReader reader)
    {
        // Debug.Log("Corner");
        XmlDocument doc = new XmlDocument();
        XmlNode readernode = doc.ReadNode(reader.ReadSubtree());
        XmlNodeList member = readernode.ChildNodes;
        Position lowerCorner = null;
        Position upperCorner = null;
        foreach (XmlNode node in member)
        {
            //Debug.Log(node.Name);
            if (node.Name == "gml:lowerCorner")
            {
                // Debug.Log("l "+node.InnerText);
                lowerCorner = getPosition(node.InnerText);
            }
            if (node.Name == "gml:upperCorner")
            {
                // Debug.Log("u "+node.InnerText);
                upperCorner = getPosition(node.InnerText);

 
            }
        }
        // Debug.Log(upperCorner+" "+lowerCorner);

        // GameObject go1   = GameObject.CreatePrimitive (PrimitiveType.Sphere);
        // GameObject go2   = GameObject.CreatePrimitive (PrimitiveType.Sphere);
        // go1.transform.parent = transform;
        // go2.transform.parent = transform;
        // go1.name = "lower";
        // go2.name = "upper";
        // go1.transform.position = lowerCorner.ToVector3(lowerCorner);
        // go2.transform.position = upperCorner.ToVector3(lowerCorner);
        return (lowerCorner, upperCorner);
    }

    // gmlファイルから角を取得
    private (Position lowerCorner, Position upperCorner) getCorner(string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;

        // string gmlPath =udxpath+ @"dem\50303564_dem_6697_op.gml";

        // string fullPath = Path.GetFullPath(gmlPath);
        // Debug.Log(fullPath);

        using (var fileStream = File.OpenText(gmlPath))
        using (XmlReader reader = XmlReader.Create(fileStream, settings))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "gml:Envelope")
                    {
                        return getCornerFromReader(reader);
                    }
                }

            }
        }
        return (null,null);
    }    


    Position getBaseLowerCorner(string gmlPath) {
        (Position lowerCorner, Position upperCorner) = getCorner(gmlPath) ;
        return lowerCorner;
        
    }





    // udxpath中のmapindexと種類(dem/tran/bldg)からファイルのパスを取得（８桁の３次メッシュの場合）
    public string getPath(string udxpath, string mapindex, string dirname) 
    {
        if (!Directory.Exists(udxpath+dirname)) {
            return null;
        }
            
        DirectoryInfo dir = new DirectoryInfo(udxpath+dirname);
        FileInfo[] info = dir.GetFiles(mapindex+@"*.gml");
        // foreach(FileInfo f in info)
        // {
        //     Debug.Log(f.Name);
        // }
        if (info.Length == 0) {
            return null;
        }
        return info[0].FullName;
    }

    // udxpath中のmapindexと種類(dem/tran/bldg)からファイルのパスを取得（６桁の２次メッシュの場合）
    public string getPath6(string udxpath, string mapindex, string dirname) 
    {
        if (mapindex.Length >= 6) {
            string mapindex6 = mapindex.Substring(0,6);
            // Debug.Log("mapindex6 "+mapindex6);
            DirectoryInfo dir6 = new DirectoryInfo(udxpath+dirname);
            FileInfo[] info6 = dir6.GetFiles(mapindex6+@"*.gml");
            if (info6.Length == 0) {
                return null;
            }
            return info6[0].FullName;
        } else {
            return null;
        }
    }

    // udxpath中のmapindexと種類(dem/tran/bldg)からファイルのパスを取得（６桁の２次メッシュの場合）
    public List<string> getPath6s(string udxpath, string mapindex, string dirname) 
    {
        List<string> filenames = new List<string>();

        if (mapindex.Length >= 6) {
            string mapindex6 = mapindex.Substring(0,6);
            // Debug.Log("mapindex6 "+mapindex6);
            DirectoryInfo dir6 = new DirectoryInfo(udxpath+dirname);
            FileInfo[] info6 = dir6.GetFiles(mapindex6+@"*.gml");
            if (info6.Length == 0) {
                return null;
            } else {
                for(int i = 0; i < info6.Length; i++) {
                    filenames.Add(info6[i].FullName);
                }
            }
            return filenames;
        } else {
            return null;
        }
    }


    // DEM用のテクスチャの画像をダウンロード　タイルごとなのでメッシュとはズレがある
    // IEnumerator
    public void 
    getImage(string mapindex, int x, int y){

        string path = Application.dataPath+@"\..\cyberjapandata\";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        string filename = path+"cyberjapandata-"+mapindex+"-"+y+"-"+x+".jpg";
        if (System.IO.File.Exists(filename))
        {
            Debug.Log("File exists!");
            
        } else {
            string url=@"https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/18/"+x+"/"+y+".jpg";
            // Debug.Log("Downloading "+url);
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                WebResponse res = req.GetResponse();
                Stream st = res.GetResponseStream();

                byte[] buffer = new byte[65535];
                MemoryStream ms = new MemoryStream();
                while (true)
                {
                    try
                    {
                        int rb = st.Read(buffer, 0, buffer.Length);
                        if (rb > 0)
                        {
                            ms.Write(buffer, 0, rb);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (IOException e)
                    {
                        Debug.Log(e.ToString());
                    }

                }
                // Debug.Log(path);
                FileStream fs = new FileStream(filename, FileMode.Create);
                byte[] wbuf = new byte[ms.Length];
                ms.Seek(0, SeekOrigin.Begin);
                ms.Read(wbuf, 0, wbuf.Length);
                fs.Write(wbuf, 0, wbuf.Length);
                fs.Close();
            }
            // finally
            catch (WebException e)
            {
                Debug.Log("Error "+e);
            }
        }
        // yield return null;//new WaitForSecondsRealtime(0.01f);                              
        // cordataImage.cor1finished = true;
    }

    // DEM用の画像をつなぎ合わせてトリミングしてTextureを返す
    //Texture2D
    // void 
    IEnumerator        
    getDemTexture8(string mapindex, TexRef tr) {
        int iyd = 0;        
        string path = Application.dataPath+@"\..\cyberjapandata\";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        string filename = path+"dem-"+mapindex + ".jpg";
        // string path = Application.dataPath;
        // string filename = path+@"\..\cyberjapandata\dem-"+mapindex + ".jpg";

        // 地域メッシュより大きな範囲のタイルの位置を取得
        GridSquareMeshCode mil = new GridSquareMeshCode(mapindex);
        // Debug.Log(mil.index);
        // Debug.Log(mil.lat+" "+mil.lon);
        GridSquareMeshCode mil2 = new GridSquareMeshCode(mil.lat,mil.lon);
        // Debug.Log(mil2.index);
        // Debug.Log(mil2.lat+" "+mil2.lon);
        // Debug.Log("----");

        GridSquareMeshCode miu = mil.add3(1,1);
        // Debug.Log(miu.index);
        // Debug.Log(miu.lat+" "+miu.lon);
        MapTile mtl = new MapTile(mil.lat, mil.lon,18);
        // Debug.Log("mtl "+mtl.x+" "+mtl.y+"         "+mtl.lat+" "+mtl.lon);
        MapTile mtl2 = new MapTile(mtl.x, mtl.y+1,18);
        // Debug.Log("mtl2 "+mtl2.x+" "+mtl2.y+"         "+mtl2.lat+" "+mtl2.lon);
        MapTile mtu = new MapTile(miu.lat, miu.lon,18);
        // Debug.Log("mtu "+mtu.x+" "+mtu.y+"         "+mtu.lat+" "+mtu.lon);
        MapTile mtu2 = new MapTile(mtu.x+1, mtu.y,18);
        // Debug.Log("mtu2 "+mtu2.x+" "+mtu2.y+"         "+mtu2.lat+" "+mtu2.lon);

        // ダウンロードする個数
        int w = (1+mtu.x-mtl.x);
        int h = (1+mtl.y-mtu.y);   

        //int count = 0;
        if (!System.IO.File.Exists(filename))
        {
            Debug.Log("Create DEM Texture");

            // 範囲の画像をダウンロード
            for(int y = mtu.y; y <= mtl.y; y++) 
            {
                for(int x = mtl.x; x <= mtu.x; x++) 
                {
                                        iyd++;
                                        if (iyd%50==0) {
                                            Debug.Log("Downloading Images for DEM "+(100*(y-mtu.y)/(mtl.y-mtu.y))+"%");
                                            yield return null;  
                                        }  

                    getImage(mapindex,x,y);
                }
            }
        
            // テクスチャに画像を貼り付け
            Texture2D texture = new Texture2D(w*256, h*256);
            for(int y = 0; y < h; y++) 
            {
                for(int x = 0; x < w; x++) 
                {
                    byte[] bytes = File.ReadAllBytes(path+"cyberjapandata-"+mapindex+"-"+(mtu.y+y)+"-"+(mtl.x+x)+".jpg");
                    Texture2D t = new Texture2D(2, 2);
                    t.LoadImage(bytes);
                    texture.SetPixels32(x * 256, (h-1-y) * 256,256, 256, t.GetPixels32());
                }
            }

            // var bytesAll = texture.EncodeToJPG();
            // File.WriteAllBytes(path+@"\..\"+mapindex + "-all.jpg", bytesAll);

            Position tileL = new Position(mtl2.lat,mtl2.lon,0);
            Position tileU = new Position(mtu2.lat,mtu2.lon,0);
            Position mapL = new Position(mil.lat, mil.lon,0);
            Position mapU = new Position(miu.lat, miu.lon,0);
            Vector3 v3tileL = tileL.ToVector3(tileL);
            Vector3 v3tileU = tileU.ToVector3(tileL);
            Vector3 v3mapL = mapL.ToVector3(tileL);
            Vector3 v3mapU = mapU.ToVector3(tileL);
            // Debug.Log("v3tileL"+v3tileL);
            // Debug.Log("v3tileU"+v3tileU);
            // Debug.Log("v3mapL"+v3mapL);
            // Debug.Log("v3mapU"+v3mapU);
            int xl = (int)(w*256*v3mapL.x/v3tileU.x);
            int xu = (int)(w*256*v3mapU.x/v3tileU.x);
            int yl = (int)(h*256*v3mapL.z/v3tileU.z);
            int yu = (int)(h*256*v3mapU.z/v3tileU.z);
            int tw = xu-xl;
            int th = yu-yl;
            // Debug.Log("wh"+w+" "+h+" "+tw+ " "+th+" "+yu+" "+yl);
            // Texture2D textureIN = new Texture2D(w*256, h*256);
            // byte[] bytesIN = File.ReadAllBytes(path+@"\..\"+mapindex+"-all.jpg");
            // textureIN.LoadImage(bytesIN);


            // 地域メッシュの部分を切り出してJPEGファイルとして保存
            Texture2D textureOut = new Texture2D(tw,th);
            Color[] pixels = texture.GetPixels(xl,yl,tw,th);
            textureOut.SetPixels(pixels);
            var bytesAllOut = textureOut.EncodeToJPG();
            File.WriteAllBytes(filename, bytesAllOut);

        }
        // ＪPEGファイルをテクスチャに読み込む
        Texture2D  textureL2 = new Texture2D(w*256, h*256);
        byte[] bytesL2 = File.ReadAllBytes(filename);
        textureL2.LoadImage(bytesL2);        
        tr.tex =  textureL2;   
        // return textureL2;
        cordataImage.cor1finished = true;            
    }    
    static Texture2D GetResized(Texture2D texture, int width, int height)
    {
        //Debug.Log("resize");
        RenderTexture prevActiveRT = RenderTexture.active;
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        RenderTexture.active = rt;

        Graphics.Blit(texture, rt);
        Texture2D resizedt2d = new Texture2D(width, height);
        resizedt2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        resizedt2d.Apply();

        RenderTexture.active = prevActiveRT;
        RenderTexture.ReleaseTemporary(rt);
        return resizedt2d;
    }

    // DEM用の画像をつなぎ合わせてトリミングしてTextureを返す
    //Texture2D 
    // void 
    IEnumerator     
    getDemTexture6(string mapindex, TexRef tr) {
        int iyd = 0;
        string path = Application.dataPath+@"\..\cyberjapandata\";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        string filename = path+"dem-"+mapindex + ".jpg";
        // string path = Application.dataPath;
        // string filename = path+@"\..\cyberjapandata\dem-"+mapindex + ".jpg";

        // 地域メッシュより大きな範囲のタイルの位置を取得
        GridSquareMeshCode mil = new GridSquareMeshCode(mapindex);
        // Debug.Log(mil.index);
        // Debug.Log(mil.lat+" "+mil.lon);
        GridSquareMeshCode mil2 = new GridSquareMeshCode(mil.lat,mil.lon);
        // Debug.Log(mil2.index);
        // Debug.Log(mil2.lat+" "+mil2.lon);
        // Debug.Log("----");

        GridSquareMeshCode miu = mil.add2(1,1);
        // Debug.Log(miu.index);
        // Debug.Log(miu.lat+" "+miu.lon);
        MapTile mtl = new MapTile(mil.lat, mil.lon,18);
        // Debug.Log("mtl "+mtl.x+" "+mtl.y+"         "+mtl.lat+" "+mtl.lon);
        MapTile mtl2 = new MapTile(mtl.x, mtl.y+1,18);
        // Debug.Log("mtl2 "+mtl2.x+" "+mtl2.y+"         "+mtl2.lat+" "+mtl2.lon);
        MapTile mtu = new MapTile(miu.lat, miu.lon,18);
        // Debug.Log("mtu "+mtu.x+" "+mtu.y+"         "+mtu.lat+" "+mtu.lon);
        MapTile mtu2 = new MapTile(mtu.x+1, mtu.y,18);
        // Debug.Log("mtu2 "+mtu2.x+" "+mtu2.y+"         "+mtu2.lat+" "+mtu2.lon);

        // ダウンロードする個数
        int w = (1+mtu.x-mtl.x);
        int h = (1+mtl.y-mtu.y);   
        int whsize = 64;

            Position tileL = new Position(mtl2.lat,mtl2.lon,0);
            Position tileU = new Position(mtu2.lat,mtu2.lon,0);
            Position mapL = new Position(mil.lat, mil.lon,0);
            Position mapU = new Position(miu.lat, miu.lon,0);
            Vector3 v3tileL = tileL.ToVector3(tileL);
            Vector3 v3tileU = tileU.ToVector3(tileL);
            Vector3 v3mapL = mapL.ToVector3(tileL);
            Vector3 v3mapU = mapU.ToVector3(tileL);
            // Debug.Log("v3tileL"+v3tileL);
            // Debug.Log("v3tileU"+v3tileU);
            // Debug.Log("v3mapL"+v3mapL);
            // Debug.Log("v3mapU"+v3mapU);
            int xl = (int)(w*whsize*v3mapL.x/v3tileU.x);
            int xu = (int)(w*whsize*v3mapU.x/v3tileU.x);
            int yl = (int)(h*whsize*v3mapL.z/v3tileU.z);
            int yu = (int)(h*whsize*v3mapU.z/v3tileU.z);
            int tw = xu-xl;
            int th = yu-yl;

        if (!System.IO.File.Exists(filename))
        {
            Debug.Log("Create DEM Texture");

            //範囲の画像をダウンロード
            for(int y = mtu.y; y <= mtl.y; y++) 
            {
                for(int x = mtl.x; x <= mtu.x; x++) 
                {
                                        iyd++;
                                        if (iyd%50==0) {
                                            Debug.Log("Downloading Images for DEM "+(100*(y-mtu.y)/(mtl.y-mtu.y))+"%");
                                            yield return null;  
                                        }                      
                    getImage(mapindex,x,y);
                    // getImage(mapindex,x,y);
                    // cordataImage.cor1finished = false;
                    // cordataImage.coroutine1 = EditorCoroutineUtility.StartCoroutine(getImage(mapindex,x,y), this);  
                    // while ( !cordataImage.cor1finished) {}
                }
            }
        
            // テクスチャに画像を貼り付け
            Texture2D texture = new Texture2D(w*whsize, h*whsize);
            for(int y = 0; y < h; y++) 
            {
                for(int x = 0; x < w; x++) 
                {
                    string tfilename = path+"cyberjapandata-"+mapindex+"-"+(mtu.y+y)+"-"+(mtl.x+x)+".jpg";
                    if (System.IO.File.Exists(tfilename))
                    {   
                        byte[] bytes = File.ReadAllBytes(tfilename);
                        Texture2D t = new Texture2D(2, 2);
//                        Texture2D t = new Texture2D(2, 2);
                        t.LoadImage(bytes);
                        Texture2D t2 = GetResized(t,whsize, whsize);
                        
                        

                        texture.SetPixels32(x * whsize, (h-1-y) * whsize,whsize, whsize, t2.GetPixels32());
                    }
                }
            }

            // var bytesAll = texture.EncodeToJPG();
            // File.WriteAllBytes(path+@"\..\"+mapindex + "-all.jpg", bytesAll);


            // Debug.Log("wh"+w+" "+h+" "+tw+ " "+th+" "+yu+" "+yl);
            // Texture2D textureIN = new Texture2D(w*256, h*256);
            // byte[] bytesIN = File.ReadAllBytes(path+@"\..\"+mapindex+"-all.jpg");
            // textureIN.LoadImage(bytesIN);


            // 地域メッシュの部分を切り出してJPEGファイルとして保存
            // Texture2D textureOut = new Texture2D(tw,th);
            Texture2D textureOut = new Texture2D(tw,th);
            Color[] pixels = texture.GetPixels(xl,yl,tw,th);
            textureOut.SetPixels(pixels);
            var bytesAllOut = textureOut.EncodeToJPG();
            File.WriteAllBytes(filename, bytesAllOut);

        }
        Debug.Log("Texture Load");
        // ＪPEGファイルをテクスチャに読み込む
//        Texture2D textureL2 = new Texture2D(tw, th);
        Texture2D textureL2 = new Texture2D(2, 2);
        byte[] bytesL2 = File.ReadAllBytes(filename);
        textureL2.LoadImage(bytesL2);            
        Texture2D textureL3 = GetResized(textureL2,8192, 8192);
        tr.tex = textureL3;
        // return textureL2;
        //return textureL3;
        cordataImage.cor1finished = true;        
    }


    // gmlファイルから地形を生成
    // private void
        IEnumerator 
         makeDEM8(string mapindex, GameObject go, Position lowerCorner, Position upperCorner, string gmlPath)
    {

                                    int iyd = 0;  

        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;

        using (var fileStream = File.OpenText(gmlPath))
        using (XmlReader reader = XmlReader.Create(fileStream, settings))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "gml:trianglePatches")
                    {
                    //    createDemMesh8(mapindex, go,  lowerCorner,  upperCorner,reader);

                        XmlDocument doc = new XmlDocument();
                        var r2 = reader.ReadSubtree();
                        XmlNode cd = doc.ReadNode(r2);
                        XmlNodeList member = cd.ChildNodes;
                        List<Surface> surfaces = new List<Surface>();
                        int count = 0;
                        foreach (XmlNode node in member)
                        {
                                        iyd++;
                                        if (iyd%100==0) {
                                            Debug.Log("makeDEM8 surfaces"+iyd/100);
                                            yield return null;  
                                        }  

                            //Debug.Log(node.Name);
                            if (node.Name == "gml:Triangle")
                            {
                                //Debug.Log(node.InnerText);
                                Surface s = new Surface();
                                string posStr = node.InnerText;
                                s.SetPositions(Position.ParseString(posStr));
                                surfaces.Add(s);
                                count++;
                                // if (count > 84990)
                                // {
                                //     Debug.Log(node.InnerText);
                                // }
                            }
                        }
                        // Debug.Log(count);
                        cordataDEM.cor2finished = false;
                        cordataDEM.coroutine2 = EditorCoroutineUtility.StartCoroutine(GenerateMesh(surfaces, mapindex, go, lowerCorner, upperCorner),this) ;            
                        while(!cordataDEM.cor2finished){yield return new WaitForSecondsRealtime(0.01f); }

                        //GenerateMesh(surfaces, mapindex, go, lowerCorner, upperCorner);




                    }

                }

            }
        }
        cordataDEM.cor1finished = true;      
        Debug.Log("makeDem8 END");        
    }

    // gmlファイルから地形を生成
    // private void 
    IEnumerator 
    makeDEM6(string mapindex, GameObject go, Position lowerCorner, Position upperCorner)
    {
                            int iyd = 0;  

        List<string> demfilenames = getPath6s(udxpath, mapindex, "dem");
        if (demfilenames != null ){

            
            List<Surface> surfaces = new List<Surface>();
            int count = 0;
            foreach (string demfilename in  demfilenames) {
                if (demfilename != null && File.Exists(demfilename)) 
                {

                    Debug.Log("makeDem6 demfilename "+demfilename);

                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.IgnoreWhitespace = true;

                    using (var fileStream = File.OpenText(demfilename))
                    using (XmlReader reader = XmlReader.Create(fileStream, settings))
                    {
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                if (reader.Name == "gml:trianglePatches")
                                {
                                    XmlDocument doc = new XmlDocument();
                                    var r2 = reader.ReadSubtree();
                                    XmlNode cd = doc.ReadNode(r2);
                                    XmlNodeList member = cd.ChildNodes;
                                    foreach (XmlNode node in member)
                                    {
                        iyd++;
                        if (iyd%100000==0) {
                            Debug.Log("makeDEM6 surfaces "+iyd/100000);
                            yield return null;  
                        }                                          
                                        //Debug.Log(node.Name);
                                        if (node.Name == "gml:Triangle")
                                        {
                                            //Debug.Log(node.InnerText);
                                            Surface s = new Surface();
                                            string posStr = node.InnerText;
                                            s.SetPositions(Position.ParseString(posStr));
                                            if (s.CheckPosition(pzero,pmax)){ // 範囲内の面のみ追加
                                                surfaces.Add(s);
                                            }
                                            count++;

                                            // yield return null; 


                                            // if (count > 84990)
                                            // {
                                            //     Debug.Log(node.InnerText);
                                            // }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Debug.Log(count);
            // GenerateMesh(surfaces, mapindex, go,  lowerCorner, upperCorner);
            cordataDEM.cor2finished = false;
            cordataDEM.coroutine2 = EditorCoroutineUtility.StartCoroutine(GenerateMesh(surfaces, mapindex, go, lowerCorner, upperCorner),this) ;            
            while(!cordataDEM.cor2finished){yield return new WaitForSecondsRealtime(0.01f); }

        }

        cordataDEM.cor1finished = true;      
        Debug.Log("makeDem6 END");
//dem.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Texture");

//        dem.GetComponent<Renderer>().material.mainTexture = tex;
        //dem.GetComponent<Renderer>().sharedMaterial.mainTexture = tex;
 
    }
    // void 
    IEnumerator 
    GenerateMesh(List<Surface> surfaces, string mapindex,GameObject go, Position lowerCorner, Position upperCorner){
            int count;
            int iyd = 0;
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            

            mesh.Clear();
            count = surfaces.Count;
            // Debug.Log(count);
            var vertices = new Vector3[ count* 3];
            var triangles = new int[count * 3];
            Vector3 uvmax = upperCorner.ToVector3(lowerCorner);

            List <Vector3>verticesList  = new List<Vector3>();
            List <Vector3>verticesListT  = new List<Vector3>();
            Dictionary<string,int> dic = new Dictionary<string,int>();  
            Dictionary<string,int> dicT = new Dictionary<string,int>();  

            Debug.Log("uvmax "+uvmax);
            Debug.Log("mapindex "+mapindex);
            Debug.Log("baseLowerCorner "+ baseLowerCorner);
            Debug.Log("lowerCorner "+ lowerCorner);
            Debug.Log("upperCorner "+ upperCorner);
            // Vector3 v3low =baseLowerCorner.ToVector3(lowerCorner);
            Vector3 v3low =lowerCorner.ToVector3(baseLowerCorner);
    //       for (int i = 0; i < surfaces.Count; i++)
            for (int i = 0; i < count; i++)
            {
                                        iyd++;
                                        if (iyd%10000==0) {
                                            Debug.Log("GenerateMesh "+(100*i/count)+"%");
                                            yield return null;  
                                        }  


                Vector3 v3a = surfaces[i].Positions[0].ToVector3(baseLowerCorner);
                Vector3 v3b = surfaces[i].Positions[2].ToVector3(baseLowerCorner);
                Vector3 v3c = surfaces[i].Positions[1].ToVector3(baseLowerCorner);
                int ia = addVertices(dic,verticesList, v3a);
                int ib = addVertices(dic,verticesList, v3b);
                int ic = addVertices(dic,verticesList, v3c);

                triangles[i * 3 + 0] = ia;
                triangles[i * 3 + 1] = ib;
                triangles[i * 3 + 2] = ic;

                Vector3 v3aT = surfaces[i].Positions[0].ToVector3(lowerCorner);
                Vector3 v3bT = surfaces[i].Positions[2].ToVector3(lowerCorner);
                Vector3 v3cT = surfaces[i].Positions[1].ToVector3(lowerCorner);
                int iaT = addVertices(dicT,verticesListT, v3aT);
                int ibT = addVertices(dicT,verticesListT, v3bT);
                int icT = addVertices(dicT,verticesListT, v3cT);
            }

            vertices = verticesList.ToArray();

            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                // uv[i] = new Vector2(1.0f*vertices[i].x / uvmax.x, 1.0f*vertices[i].z / uvmax.z);
                uv[i] = new Vector2(1.0f*(vertices[i].x -v3low.x)/ uvmax.x, 1.0f*(vertices[i].z-v3low.z) / uvmax.z);
            }



            if (demAdjustRoad){
                for (int i = 0; i <vertices.Length; i++)
                {
                    float difflen = 5;// Meshの間隔は5mぐらいか
                    float [,] posdiff = new float[5,2]{{0,0},{-1,-1},{-1,1},{1,1},{1,-1}};//中心以外に4か所チェック
                    float min = vertices[i].y;
                    for(int j = 0; j < posdiff.GetLength(0); j++) {
                        //Debug.Log("posdiff.length "+posdiff.GetLength(0)+" "+j);
                        Ray ray = new Ray(new Vector3(vertices[i].x+posdiff[j,0]*difflen, vertices[i].y, vertices[i].z+posdiff[j,1]*difflen), -Vector3.up);
                        RaycastHit hit = new RaycastHit();
                        if (Physics.Raycast(ray, out hit))
                        {
                            if (hit.point.y < min)
                            min = hit.point.y;
                        }
                        // iyd++;
                        // if (iyd%100==0) {
                        //     yield return null;  
                        // }                          
                    }
                    vertices[i].y = min-0.05f;
                }
            }
            TexRef tr = new TexRef();
            Texture2D tex = null;
            Debug.Log("mapindex.Length "+mapindex.Length+" "+mapindex);

            cordataImage.cor1finished = false;
            if (mapindex.Length == 6) {
                cordataImage.coroutine1 = EditorCoroutineUtility.StartCoroutine(getDemTexture6(mapindex,tr), this);  
            } else {
                cordataImage.coroutine1 = EditorCoroutineUtility.StartCoroutine(getDemTexture8(mapindex,tr), this);  
            }
            while ( !cordataImage.cor1finished) {
                yield return null;  
            }             
            tex = tr.tex;

            // Debug.Log(tex);
            GameObject dem = new GameObject("Dem");
            dem.AddComponent<MeshFilter>();
            dem.AddComponent<MeshRenderer>();
            dem.AddComponent<MeshCollider>();

            dem.transform.parent = go.transform;

            // Debug.Log(surfaces.Count+" "+count);
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();

            var filter = dem.GetComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            if (demUseCollider) {
                var collider = dem.GetComponent<MeshCollider>();
                collider.sharedMesh  = mesh;
            }
            string saveMeshdirPath = @"Assets\Resources\Mesh\"+mapindex;
            if (!Directory.Exists(saveMeshdirPath)) Directory.CreateDirectory(saveMeshdirPath);
            if (saveMeshAsAsset){
                string saveMeshFilename = saveMeshdirPath+@"\dem-mesh-"+mapindex+".asset";
                // UnityEditor.AssetDatabase.CreateAsset(filter.sharedMesh,saveMeshFilename );      
                Mesh assetM2 = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(saveMeshFilename);
                if( assetM2 == null){
        //            AssetDatabase.CreateAsset(newAsset, path);
                    UnityEditor.AssetDatabase.CreateAsset(filter.sharedMesh,saveMeshFilename );
                } else {
    //                filter.sharedMesh = assetM2;
                    UnityEditor.AssetDatabase.DeleteAsset(saveMeshFilename);
                    UnityEditor.AssetDatabase.CreateAsset(filter.sharedMesh,saveMeshFilename );
                    UnityEditor.AssetDatabase.SaveAssets();                
                }
    //             else {
    //                 UnityEditor.AssetDatabase.DeleteAsset(saveMeshFilename);
    // //                EditorUtility.CopySerialized(newAsset,asset);
    //                 UnityEditor.AssetDatabase.CreateAsset(filter.sharedMesh,saveMeshFilename );
    //                 UnityEditor.AssetDatabase.SaveAssets();
    //             }

            }
            var meshRenderer = dem.GetComponent<MeshRenderer>();
    //        dem.GetComponent<Renderer>().material.shader = Shader.Find("Legacy Shaders/Diffuse");
            //Shader sh = dem.GetComponent<MeshRenderer>().material.shader;
            //Shader sh = Shader.Find("Unlit/Texture");
            Material[] mats = new Material[1];
            // mats[0] = new Material(Shader.Find ("Universal Render Pipeline/Unlit/Texture"));
            mats[0] = new Material(Shader.Find ("Unlit/Texture"));
            if (demUseTexture) {
                mats[0].mainTexture = tex;
            // mats[0].shader = Shader.Find("Unlit/Texture");
            } else {
                mats[0] = demMaterial;
            }
            meshRenderer.materials = mats;
            // string saveTextureFilename = saveMeshdirPath+@"\dem-texture-"+mapindex+".asset";
            // UnityEditor.AssetDatabase.CreateAsset(tex,saveTextureFilename );  
            

            string savefilename =  saveMeshdirPath+@"\dem-texture-"+mapindex+".asset";
            var assetT = UnityEditor.AssetDatabase.LoadAssetAtPath(savefilename,typeof(Texture));
            if( assetT == null){
                UnityEditor.AssetDatabase.CreateAsset( tex, savefilename);
            }    


            //string saveMatFilename = saveMeshdirPath+@"\dem-mat-"+mapindex+".asset";
            //UnityEditor.AssetDatabase.CreateAsset(mats[0],saveMatFilename );        
            savefilename =  saveMeshdirPath+@"\dem-mat-"+mapindex+".asset";
            var assetM = UnityEditor.AssetDatabase.LoadAssetAtPath(savefilename,typeof(Material));
            if( assetM == null){
                UnityEditor.AssetDatabase.CreateAsset(mats[0], savefilename);
            }  
        cordataDEM.cor2finished = true;            
    }
    // メッシュの同じ点を辞書で統合    
    int addVertices(Dictionary<string,int> dic, List <Vector3> listv3, Vector3 v3) {
        string key = v3.x.ToString()+"-"+v3.y.ToString()+"-"+v3.z.ToString();
        int diccount = dic.Count;
        if (dic.ContainsKey(key)) {  
            return dic[key];  
        } 
        dic.Add(key,diccount);
        listv3.Add(v3);
        return diccount;

        // int i = listv3.Count;
        // listv3.Add(v3);
        // return i;
    }






    IEnumerator CreateRoad6(GameObject goTranLOD2, bool useArray){
                    int iyd = 0;        
        // Position lowerCorner, upperCorner;
        Debug.Log("CreateRoad6 Start");                

        // GameObject go = new GameObject(name);
        // go.transform.parent = goPLATEAUmakeDEM8(.transform;
//        GridSquareMeshCode mi6upperBlock = mizero.add3(zsize-1,xsize-1); // ３次メッシュでの右上位置ブロックのコードを取得
        GridSquareMeshCode mi6upperBlock = mizero.add3(zsize-1,xsize-1); // ３次メッシュでの右上位置ブロックのコードを取得
        Debug.Log("mi6upperBlock.index: "+ mi6upperBlock.index);
        GridSquareMeshCode mizero3 = new GridSquareMeshCode(mizero.index2); // ３次メッシュでの右上位置ブロックのコードを取得
        GridSquareMeshCode mi6upperBlock3 = new GridSquareMeshCode(mi6upperBlock.index2);
        Debug.Log("mizero3.index: " + mizero3.index+" mizero3.index2: "+mizero3.index2);
        Debug.Log("mi6upperBlock3.index: "+ mi6upperBlock3.index+" mi6upperBlock3.index2: "+mi6upperBlock3.index2);
        (int zsize6, int xsize6) = mi6upperBlock3.diff2(mizero3);// ２次メッシュでのブロック数を取得
        Debug.Log("diff xsize6: "+xsize6+" zsize6:"+zsize6);

        for(int z = 0; z <= zsize6; z++) 
        {
            for(int x = 0; x <= xsize6; x++) 
            {
                        iyd++;
                        if (iyd%100==0) {
                            yield return null;  
                        }                  
                GridSquareMeshCode mi = mizero.add2(z,x);
                string currentMapIndex = mi.index2;
                currentcreating = currentMapIndex;
                GameObject goCurrent = new GameObject(currentMapIndex);
                goCurrent.transform.parent = goTranLOD2.transform;
                Debug.Log("CreateRoad6 currentMapIndex "+currentMapIndex);                

                var corners =  getCornerFromIndex2(currentMapIndex);

                // // GridSquareMeshCode miL = new GridSquareMeshCode(mizero.index2);
                // GridSquareMeshCode miL = new GridSquareMeshCode(mi.index2);
                // GridSquareMeshCode miU = miL.add2(1,1);

                // lowerCorner = new Position(miL.lat, miL.lon,0); //２次メッシュでのブロック全体の左下
                // upperCorner = new Position(miU.lat, miU.lon,0); //２次メッシュでのブロック全体の右上  



                                string tranfilename = getPath6(udxpath, currentMapIndex, "tran");
                                Debug.Log(tranfilename);
                                if (tranfilename != null && File.Exists(tranfilename))
                                {
                                    List<Relief> surfaces = readTRANRelief(currentMapIndex, goCurrent, corners.lowerCorner, corners.upperCorner,maxheight, tranfilename);
                                    yield return null;                              
                                    cordataTRAN.cor1finished = false;
                                    cordataTRAN.coroutine1 = EditorCoroutineUtility.StartCoroutine(makeTran(currentMapIndex, goCurrent, x,z,corners.lowerCorner, corners.upperCorner,maxheight, surfaces, useArray), this);  
                                    Debug.Log("cordataTRAN.cor1finished true"+cordataTRAN.cor1finished);                          
                                    while ( !cordataTRAN.cor1finished) {
                                                //Debug.Log("cordataTRAN.cor1finished true"+cordataTRAN.cor1finished);     
                                                yield return new WaitForSecondsRealtime(0.01f);                              
                                    }             
                                    yield return new WaitForSecondsRealtime(1f);      
                                } 

                // makeDEM6(currentMapIndex, goCurrent, lowerCorner, upperCorner);

            }
        }
        // return go;
        cordataRoad6.cor1finished = true;    
        Debug.Log("CreateRoad6 End");             
    }

    IEnumerator CreateRoad8(GameObject goTranLOD2, bool useArray){
        Debug.Log("CreateRoad8");
                        for(int z = 0; z < zsize; z++) 
                        {
                            for(int x = 0; x < xsize; x++) 
                            {
                                // 対象のmapindexを作成し、その左下と右上のPositionを取得
                                Debug.Log(z+" "+x);
                                GridSquareMeshCode mi = mizero.add3(z,x);
                                string currentMapIndex = mi.index;
                                currentcreating = currentMapIndex;
                                GameObject goCurrentTranLOD2 = new GameObject(currentMapIndex);
                                goCurrentTranLOD2.transform.parent = goTranLOD2.transform;
                                Debug.Log(currentMapIndex);
                                var corners =  getCornerFromIndex3(currentMapIndex);

                                // mapindexからファイル名を作り道路を生成
                                string tranfilename = getPath(udxpath, currentMapIndex, "tran");
                                Debug.Log(tranfilename);
                                if (tranfilename != null && File.Exists(tranfilename))
                                {
                                    List<Relief> surfaces = readTRANRelief(currentMapIndex, goCurrentTranLOD2, corners.lowerCorner, corners.upperCorner,maxheight, tranfilename);
                                    yield return null;                              
                                    cordataTRAN.cor1finished = false;
                                    cordataTRAN.coroutine1 = EditorCoroutineUtility.StartCoroutine(makeTran(currentMapIndex, goCurrentTranLOD2, x,z,corners.lowerCorner, corners.upperCorner,maxheight, surfaces, useArray), this);  
                                    Debug.Log("cordataTRAN.cor1finished true"+cordataTRAN.cor1finished);                          
                                    while ( !cordataTRAN.cor1finished) {
                                                //Debug.Log("cordataTRAN.cor1finished true"+cordataTRAN.cor1finished);     
                                                yield return new WaitForSecondsRealtime(0.01f);                              
                                    }             
                                    yield return new WaitForSecondsRealtime(1f);      
                                } 
                                // currentcreating = "next";
                                // iyd++;
                                // Debug.Log("End in loop");                                        
                            }
                        // }

                    }    
        cordataRoad8.cor1finished = true;    
        Debug.Log("CreateRoad8 End");                    
    }

    void makeHeightArray() {
                        for(int z = 0; z < zsize; z++) 
                        {
                            for(int x = 0; x < xsize; x++) 
                            {
                                Debug.Log(z+" "+x);
                                //yield return null;  
                                GridSquareMeshCode mi = mizero.add3(z,x);
                                string currentMapIndex = mi.index;
//                                var corners = getCorner(demfilename);
                                // var corners =  getCornerFromIndex3(currentMapIndex);                                  
                                // Vector3 v3u = corners.upperCorner.ToVector3(baseLowerCorner);
                                // if (v3u.y > maxheight) {
                                //     maxheight = v3u.y;
                                // }                        
                                // string demfilename = getPath(udxpath, currentMapIndex, "dem");
                                // Debug.Log(demfilename);
                                // if (demfilename != null && File.Exists(demfilename))
                                // {
                                // // 地形の高さの配列を作成
                                // makeHeightArrayFromDEMGML(currentMapIndex, tmpDem, x,z, corners.lowerCorner, corners.upperCorner, maxheight, demfilename);
                                // }
                                makeHeightArray1Block(x,z,currentMapIndex);
                                currentcreating = currentMapIndex;
                            }
                        }
    }

    void makeHeightArray1Block(int hx, int hz ,string currentMapIndex)
    {
            var corners =  getCornerFromIndex3(currentMapIndex);              
            Vector3 upperCornerV3 = corners.upperCorner.ToVector3(corners.lowerCorner);
            Vector3 baseV3 = corners.lowerCorner.ToVector3(baseLowerCorner);
            for (int ix = 0; ix < heightarraysize; ix++)
            for (int iz = 0; iz < heightarraysize; iz++)
            {
                float x = baseV3.x + upperCornerV3.x * (ix+0.5f) / heightarraysize;
                float z = baseV3.z + upperCornerV3.z * (iz+0.5f) / heightarraysize;
                Ray ray = new Ray(new Vector3(x, rayHeight, z), -Vector3.up);
                RaycastHit hit = new RaycastHit();
                float y = 0;
                if (Physics.Raycast(ray, out hit))
                {
                    y = hit.point.y;
                } else {
                    //Debug.Log("makeHeightArrayFromDEM height0");
                    float [,] posdiff = new float[5,2]{{0,0},{-1,-1},{-1,1},{1,1},{1,-1}};//中心以外に4か所チェック
                    float difflen = 1f;
                    for(int j = 0; j < posdiff.GetLength(0); j++) {
                        //Debug.Log("posdiff.length "+posdiff.GetLength(0)+" "+j);
                        
//                        ray = new Ray(new Vector3(x+posdiff[j,0]*difflen, maxheight + 30, z+posdiff[j,1]*difflen), -Vector3.up);
                        ray = new Ray(new Vector3(x+posdiff[j,0]*difflen, rayHeight, z+posdiff[j,1]*difflen), -Vector3.up);
                        hit = new RaycastHit();
                        if (Physics.Raycast(ray, out hit))
                        {
                           y = hit.point.y;
                           // Debug.Log("makeHeightArrayFromDEM hit");
                           break;
                        }
                    }                    
                }
                heightArray[hx*heightarraysize+ ix, hz*heightarraysize+ iz] = y;
            }

    }  


    void makeRoadArray() {
        for(int z = 0; z < zsize; z++) 
        {
            for(int x = 0; x < xsize; x++) 
            {
                Debug.Log(z+" "+x);
                //yield return null;  
                GridSquareMeshCode mi = mizero.add3(z,x);
                string currentMapIndex = mi.index;
                makeRoadArray1Block(x,z,currentMapIndex);
                currentcreating = currentMapIndex;
            }
        }
    }

    void makeRoadArray1Block(int hx, int hz ,string currentMapIndex)
    {
            var corners =  getCornerFromIndex3(currentMapIndex);              
            Vector3 upperCornerV3 = corners.upperCorner.ToVector3(corners.lowerCorner);
            Vector3 baseV3 = corners.lowerCorner.ToVector3(baseLowerCorner);
        // 道路があるかどうかの配列を作成



        // 高さを測量
        //string path = Application.dataPath + "/road.Log"; //ファイルに書込み
        //Debug.Log(path);
        //bool isAppend = false; // 上書き or 追記
        //using (var fs = new StreamWriter(path, isAppend, System.Text.Encoding.GetEncoding("UTF-8")))
        {
            // Vector3 upperCornerV3 = upperCorner.ToVector3(lowerCorner);
            for (int ix = 0; ix < heightarraysize; ix++)
            for (int iz = 0; iz < heightarraysize; iz++)
            {
                float x = upperCornerV3.x * (ix+0.5f) / heightarraysize;
                float z = upperCornerV3.z * (iz+0.5f) / heightarraysize;
                Ray ray = new Ray(new Vector3(x, maxheight + 30, z), -Vector3.up);
                RaycastHit hit = new RaycastHit();
                //float y = 0;
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider.gameObject.name.Length > 3 && hit.collider.gameObject.name.Substring(0, 4) == "Road"){
                        roadArray[hx*heightarraysize+ ix, hz*heightarraysize+iz] = 1;
                        //roadArrayTmp[ix,iz] = 1;
                        float y = hit.point.y;
                        
                    }
                }
            }


            // テキストマップ　デバッグ用
            /*
            for (int iy = 0; iy < heightarraysize; iy++)
            for (int ix = 0; ix < heightarraysize; ix++)
            {   
                fs.Write("heightarray: " + ix +" "+iy+" "+heightarrayRoad[ix,iy]+"\n");
            }
            for (int iy = 0; iy < heightarraysize; iy++) {
                for (int ix = 0; ix < heightarraysize; ix++)
                {   
                    fs.Write(heightarrayRoad[ix,iy]);
                }
                fs.Write("\n");
            } 
            */

        }

    }

    void getAverage()
    {

        
        int[,] roadArrayTmp = new int[heightArray.GetLength(0), heightArray.GetLength(1)];
        //周りも道路扱い？
        for (int ix = 0; ix < heightArray.GetLength(0); ix++)
        for (int iz = 0; iz < heightArray.GetLength(1); iz++)
        {
           
            roadArrayTmp[ix,iz] = roadArray[ix,iz] ;
            int [,] posdiff = new int[5,2]{{0,0},{-1,-1},{-1,1},{1,1},{1,-1}};
            for(int j = 0; j < posdiff.GetLength(0); j++) {
                int cix = ix + posdiff[j,0];
                int ciz = iz + posdiff[j,1];
                if (0 <= cix && cix < heightarraysize && 0 <= ciz && ciz < heightarraysize) {
                    if (roadArray[cix,ciz]==1) {
                        roadArrayTmp[ix,iz] = 1;
                        break;
                    }
                }
            }
            // if(true&& roadArray[ix,iz] == 1){
            //     float x = upperCornerV3.x * (ix+0.5f) / heightarraysize;
            //     float z = upperCornerV3.z * (iz+0.5f) / heightarraysize;                    
            //     GameObject got   = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            //     got.transform.parent = go.transform;
            //     got.transform.position = new Vector3(x,heightArray[ix,iz],z);
            //     got.name = "pos"+ix+","+iz;
            // }
        }
        roadArray = roadArrayTmp;



        //string path = Application.dataPath + "/avg.Log"; //ファイルに書込み
        // bool isAppend = false; // 上書き or 追記
        //Debug.Log(path);
        //using (var fs = new StreamWriter(path, isAppend, System.Text.Encoding.GetEncoding("UTF-8")))
        {
           // fs.Write("upperCorner2 " + "[" + upperCorner2.x + " , " + upperCorner2.z + "] " + heightarraysize+"\n");
            int ix = 0;
            int iz = 0;

            for (int x = 0; x < heightArray.GetLength(0); x++)
            for (int z = 0; z < heightArray.GetLength(1); z++)
            {
                float avgsum = 0;
                int avgcount = 0;
                float avg = 0;
                // int dx = 0;
                // int dy = 0
                
                if (roadArray[x,z] == 1) {
                    // 平均を計算
                    float avgmax = heightArray[x, z];
                    float avgmin = heightArray[x, z];
                    for (int dx = -avgsize; dx <= avgsize; dx++)
                    for (int dz = -avgsize; dz <= avgsize; dz++)
                    {   
                        ix = x + dx;
                        iz = z + dz;
                        if (0 <= ix && ix <heightarraysize && 0 <= iz && iz < heightarraysize && roadArray[ix,iz] == 1) {
                            avgsum +=  heightArray[ix, iz];    
                            //fs.Write("heightarray: " + ix +" "+iy+" "+heightarray[ix, iy]+"\n");                   
                            avgcount ++;
                            if (heightArray[ix, iz] > avgmax) {
                                avgmax = heightArray[ix, iz];
                            }
                            if (heightArray[ix, iz] < avgmin) {
                                avgmin = heightArray[ix, iz];
                            }
                        }
                    }
                    if (avgcount > 0){
                        avg = avgsum/avgcount;
                    }
                    float avghigh = (avgmax + avg*3) /4;
                    float avglow = (avgmin + avg*3) / 4;
                    //fs.Write("heightarray: " + ix +" "+iy+" "+avg+" avgsum "+avgsum+" avgcount "+avgcount+"\n");
                    
                    // 最大値や最小値を考慮して平均を計算し直す（極端な値を除外）
                    // 最小値と平均値の間以上最大値と平均値の間以下のみの平均とか？
                    avgcount = 0;
                    avgsum = 0;
                    //avg = 0;
                    for (int dx = -avgsize; dx <= avgsize; dx++)
                    for (int dz = -avgsize; dz <= avgsize; dz++)
                    {   
                        ix = x + dx;
                        iz = z + dz;
                        if (0 <= ix && ix <heightarraysize && 0 <= iz && iz < heightarraysize && roadArray[ix,iz] == 1) {
                            if ( avglow <= heightArray[ix, iz] && heightArray[ix, iz] <= avghigh) {
                                avgsum +=  heightArray[ix, iz];    
                                //fs.Write("heightarray avg: " + ix +" "+iy+" "+heightarray[ix, iy]+"\n");                   
                                avgcount ++;
                            }
                        }
                    }
                    if (avgcount > 0){
                        avg = avgsum/avgcount;
                    }


                    //fs.Write("heightarray avg : " + ix +" "+iy+" "+avg+" avgsum "+avgsum+" avgcount "+avgcount+"\n");
                    heightAverageArray[x,z]= avg;
                } else {
                    heightAverageArray[x,z]= heightArray[x, z];
                }
            }
        }
        heightArray = heightAverageArray;
    }
    // gmlファイルから道路のSurfaceを取得
    List<Relief> readTRANRelief(string currentMapIndex, GameObject goCurrent, Position lowerCorner, Position upperCorner, float maxheight, string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;
        List<Relief> surfaces = new List<Relief>();
        using (var fileStream = File.OpenText(gmlPath))
        using (XmlReader reader = XmlReader.Create(fileStream, settings))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "core:CityModel")
                    {
                        surfaces = readRoadReliefFromReader( currentMapIndex,  goCurrent,  lowerCorner,  upperCorner, maxheight, reader);
                    }
                }

            }
        }
        return surfaces;
    }    

    // LOD1道路のSurfaceを取得
    List<Relief> readRoadReliefFromReader(string currentMapIndex, GameObject go, Position lowerCorner, Position upperCorner, float maxheight,XmlReader reader)
    {
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start(); //計測開始
        var sw2 = new System.Diagnostics.Stopwatch();
        sw2.Start(); //計測開始 

        Vector3 uvmax = upperCorner.ToVector3(lowerCorner);
        //Debug.Log("Road currentMapIndex "+currentMapIndex+" lowerCorner "+ lowerCorner+" upperCorner "+upperCorner+" uvmax "+uvmax);

        // GameObject road = new GameObject("Tran");
        // road.transform.parent = go.transform;

        XmlDocument doc = new XmlDocument();
        var r2 = reader.ReadSubtree();
        XmlNode cd = doc.ReadNode(r2);
        XmlNodeList member = cd.ChildNodes;
        List<Relief> surfaces = new List<Relief>();
        int index1 = 0;
        string tranid = "noTranid";
        foreach (XmlNode node in member)
        {
            
                      //Debug.Log(node.Name);
            if (node.Name == "core:cityObjectMember")
            {
                if (true) //(index1 == 14283) //(true) //(index1 <= 26) // (true) //index1 == 26)// 2) // 26)
                {
                    Relief relief = new Relief();
                    List<Surface> ss = new List<Surface>();

                    Debug.Log("Road " + index1);
                    Debug.Log(node.InnerText);
                    Debug.Log(node.FirstChild.Name);
                    if (node.FirstChild.Attributes.Item(0) !=  null) {
                        Debug.Log(node.FirstChild.Attributes.Item(0).Name);
                        Debug.Log(node.FirstChild.Attributes.Item(0).Value);
                        tranid = node.FirstChild.Attributes.Item(0).Value;
                    } else {
                        tranid = "tran"+index1;
                        Debug.Log("node.FirstChild.Attributes.Item(0) null");
                    }
                    relief.Id = tranid;
                    relief.Name = tranid;
                    // if (tranid != "tran_78dcce53-b5da-406a-88d4-1c04f32ff1aa") {
                    //     continue;
                    // }
                    
                    // if (tranid != "tran_d1615ca3-0bab-43ec-b7ac-7d917a766faf") { //tfa_84ade7da-fa41-451b-b142-bb22bcab2c8a
                    //     continue;
                    // }
                    Debug.Log(node.FirstChild.Name); //tran.road
                    XmlNodeList tranMembers = node.FirstChild.ChildNodes;
                    foreach (XmlNode node2 in tranMembers)
                    {
                        if (node2.Name == "tran:lod1MultiSurface") 
                        {

                            XmlNodeList surfaceMembers = node2.FirstChild.ChildNodes;
                            Debug.Log("surfaceMembers.Count "+surfaceMembers.Count);
                            // if (surfaceMembers.Count==0) {
                            //     Debug.Log(node.FirstChild.FirstChild.FirstChild.InnerText);
                            // } else 
                            {
                                //int index2 = 0;
                                foreach (XmlNode polygon in surfaceMembers) //<gml:Polygon>
                                {
                                    foreach (XmlNode subnode in polygon.FirstChild.ChildNodes) // gml:exterior 
                                    {
                                        Debug.Log("subnode.Name "+subnode.Name);
                                        string posStr = subnode.InnerText;
                                        if (tranid == "tran_6a3300c9-3236-45eb-b2b6-802be8bfda3b") {
                                            Debug.Log("tran_6a3300c9-3236-45eb-b2b6-802be8bfda3b "+posStr);
                                        }
                                        if (tranid == "tran_c144d4b1-d15c-47c7-8e5b-6cbc92f84f3f") {
                                            Debug.Log("tran_c144d4b1-d15c-47c7-8e5b-6cbc92f84f3f "+posStr);
                                        }                                        
    //                                    Debug.Log("subnode "+" "+tranid+" "+posStr);
                                        // if (subnode.Name != "gml:surfaceMember"){
                                        //     continue;
                                        // }
                                        if (true) //index2 == 2)
                                        {
                                            Surface s = new Surface();
                                            // GameObject roadX = new GameObject("Road" + index1.ToString("000000") + "-" + index2.ToString("00"));
                                            // roadX.transform.parent = road.transform;
                                            // roadX.AddComponent<MeshFilter>();
                                            // roadX.AddComponent<MeshRenderer>();
                                            // roadX.AddComponent<MeshCollider>();
                                            s.SetPositions(Position.ParseString(posStr));
                                            if (s.CheckPosition(pzero,pmax)){ // 範囲内の面のみ追加
                                                ss.Add(s);
                                            }
                                            // ss.Add(s);
                                        }
                                    }
                                }
                            }

                        }
                    }
                    Debug.Log(node.FirstChild.FirstChild.Name); // tran:lod1
                    Debug.Log(node.FirstChild.FirstChild.FirstChild.Name); // gml:Muilti
                    relief.LOD1Solid = ss.ToArray();
                    surfaces.Add(relief);
//33.66826905557669 130.4468596935091 0 33.66829424994771 130.44693613891596 0 33.66831049929514 130.446983638448 0 33.66832905581547 130.44703594343642 0 33.66836363878965 130.44713249933048 0 
//33.66826905557669 130.4468596935091 0 33.66829424994771 130.44693613891596 0 33.66831049929514 130.446983638448 0 33.66832905581547 130.44703594343642 0
//tran_78dcce53-b5da-406a-88d4-1c04f32ff1aa
                }

            }
            index1++;
        }
        return surfaces;
    }       




    // LOD1の道路を生成
    // roadtype 8 6 
    // useArrayの値で切り替え
    IEnumerator
    makeTran(string currentMapIndex, GameObject go,int hx, int hz, Position lowerCorner, Position upperCorner, float maxheight,List<Relief> surfaces, bool useArray)
    {
        int iyd = 0;  

        var sw = new System.Diagnostics.Stopwatch();
        sw.Start(); //計測開始
        var sw2 = new System.Diagnostics.Stopwatch();
        sw2.Start(); //計測開始 

        Vector3 uvmax = upperCorner.ToVector3(lowerCorner);
        //Debug.Log("Road currentMapIndex "+currentMapIndex+" lowerCorner "+ lowerCorner+" upperCorner "+upperCorner+" uvmax "+uvmax);

        GameObject road = new GameObject("Tran");
        road.transform.parent = go.transform;



        int index1 = 0;
        foreach (Relief relief in surfaces)
        {
            // string roadname = relief.Name;
            foreach(Surface s in relief.LOD1Solid)
            {
                string roadname = relief.Name+"-"+ index1.ToString("000000");
//                string roadname = "Road" + index1.ToString("000000");
                GameObject roadX = new GameObject(roadname);
                roadX.transform.parent = road.transform;
                roadX.AddComponent<MeshFilter>();
                roadX.AddComponent<MeshRenderer>();
                roadX.AddComponent<MeshCollider>();
                
                int len = s.Positions.Length;
                // Debug.Log("len " + len);
                Vector3[] vertices = new Vector3[len];
                List<Vector3> verticesList = new List<Vector3>();

                // 範囲上になければ次へ進む(continue) 地面が３次メッシュで道路が２次メッシュの場合への対応
                //bool inmap = false;
                for (int i = 0; i < len; i++)
                {
                    Vector3 v = s.Positions[i].ToVector3(baseLowerCorner);
                    // if (lowerCorner.Latitude < s.Positions[i].Latitude && 
                    // s.Positions[i].Latitude < upperCorner.Latitude  && 
                    // lowerCorner.Longitude < s.Positions[i].Longitude && 
                    // s.Positions[i].Longitude < upperCorner.Longitude
                    // ) {
                    //     inmap = true;
                    // }
                    verticesList.Add(v);
                }
                // if (!inmap) {
                //     continue;
                // }
//                            verticesList.Add(s.Positions[0].ToVector3(lowerCorner)); //起点


                //Debug.Log( "index1: " + index1 + " verticesList "+verticesList.Count);
                // if (verticesList.Count <=4 ){
                //     continue;
                // }

 
                // 近くの点をまとめたり遠い点はあいだに点を追加したり
                List<Vector3> verticesList2 = new List<Vector3>();
                for(int i = 0; i <= verticesList.Count; i++) // Count+1まででループにしておく（その必要があるかは未検証）
                {
                    Vector3 v = verticesList[i%verticesList.Count];
                    if (i==0 ){
                        verticesList2.Add(v);
                    }else {
                        Vector3 vprev = verticesList[i-1];
                        float distance = Vector3.Distance(v, vprev);
                        // Debug.Log("distance "+distance);
                        if (Vector3.Distance(v, vprev) > roadLOD1MargeLength)  { 
                            if (roadLOD1SplitLength> 0) {
                                // 長い線は分割(hiro)
                                Vector3 diff = v - vprev;  //距離の差分
                                int count = (int)(distance / roadLOD1SplitLength); // roadSplitLength m おき
                                if (count < 10000) {
                                    for(int j = 1; j < count; j++)
                                    {
                                        verticesList2.Add(vprev + diff*j / count);
                                    }
                                }
                            }
                            verticesList2.Add(v);
                        }                                    
                    }

                }
                // Debug.Log("verticesList2 "+verticesList2.Count);


                // 高さを決める
                List<Vector3> verticesList3 = new List<Vector3>();
                for(int i = 0; i < verticesList2.Count; i++) 
                {


                    Vector3 v = verticesList2[i];
                    Vector3 v2 = new Vector3(v.x, uvmax.y + 10, v.z);

                    // 位置を見て確認するためのデバッグ用 ///////////////////////////////////////
                    bool debug = false;
                    if (debug) {
                        GameObject got   = GameObject.CreatePrimitive (PrimitiveType.Sphere);
                        got.transform.parent = road.transform;
                        got.transform.position = v;
                        got.name = "pos"+i;
                    }
                    // 配列で高さを決定
                    if (useArray) {
                        Vector3 upperCorner2 = upperCorner.ToVector3(lowerCorner);
                        Vector3 baseV3 = lowerCorner.ToVector3(baseLowerCorner);


                        //Debug.Log("createRoadFromReader");
                        int ix = (int)((v.x-baseV3.x) / upperCorner2.x * heightarraysize);
                        int iz = (int)((v.z-baseV3.z) / upperCorner2.z * heightarraysize);
                        if (ix <0 ) ix = 0;
                        if (iz <0 ) iz = 0;
                        if (ix > heightarraysize-1) ix = heightarraysize-1;
                        if (iz > heightarraysize-1) iz = heightarraysize-1;
                        v.y = heightArray[hx*heightarraysize+ix,hz*heightarraysize+ iz];
                    }

                    // 地面で高さを決定 地面に向けてRaycastを飛ばし
                    else{
                        Ray ray = new Ray(new Vector3(v.x, rayHeight , v.z), -Vector3.up);
                        RaycastHit hit = new RaycastHit();
                        if (Physics.Raycast(ray, out hit, rayHeight ))
                        {
                            v.y = hit.point.y + 0.1f;
                            //Debug.Log(" hit.point "+hit.point);
                        } else
                        {
                            //Debug.Log("not hit");
                            //Debug.Log("createRoadFromReader height0");
                            float [,] posdiff = new float[5,2]{{0,0},{-1,-1},{-1,1},{1,1},{1,-1}};//中心以外に4か所チェック
                            float difflen = 1f;
                            for(int j = 0; j < posdiff.GetLength(0); j++) {
                                //Debug.Log("posdiff.length "+posdiff.GetLength(0)+" "+j);
//                                ray = new Ray(new Vector3(v.x+posdiff[j,0]*difflen, maxheight + 30, v.z+posdiff[j,1]*difflen), -Vector3.up);
                                ray = new Ray(new Vector3(v.x+posdiff[j,0]*difflen, rayHeight, v.z+posdiff[j,1]*difflen), -Vector3.up);
                                hit = new RaycastHit();
                                if (Physics.Raycast(ray, out hit))
                                {
                                    v.y = hit.point.y;
                                    //Debug.Log("createRoadFromReader hit");
                                break;
                                }
                            }                                    
                        }
                    Debug.DrawRay(ray.origin, ray.direction * uvmax.y * 3, Color.red, 5);
                    }

                    verticesList3.Add(v);
                                            iyd++;
                                            if (iyd%100==0) {
                                                Debug.Log("makeTran8 "+iyd);
                                                Debug.Log(" index1: "+index1+" / "+surfaces.Count+ " verticesList3: "+verticesList3.Count);
                                                Debug.Log("i "+ i +"  verticesList2.Count "+verticesList2.Count+" roadname: "+roadname+" currentMapIndex"+currentMapIndex);
                                                Debug.Log("verticesList.Count "+verticesList.Count);
                                            }
                                            yield return null;  

                }
                Debug.Log(" index1: "+index1+" / "+surfaces.Count+ " verticesList3: "+verticesList3.Count);

                                        //}  
                Debug.Log("3a");
                if (verticesList3.Count <=3){
                    continue;
                }
                Debug.Log("3b");
                // 耳刈り取り法 で 多角形の三角形分割 耳なし芳一は関係ない
                vertices = verticesList3.ToArray();
                len = vertices.Length;
                // Debug.Log("len " + len);
                Vector2[] verticesXZ = new Vector2[len];
                for (int i = 0; i < len; i++)
                {
                    Vector3 pos = vertices[i];
                    verticesXZ[i] = new Vector2(pos.x, pos.z);
                }
                // MapTriangulator tr = new MapTriangulator(verticesXZ, new Vector3(0, 0, -100));
                // int[] indices = tr.Triangulate();
                int[] indices = Triangulate(verticesXZ);
                // Debug.Log(indices.Length);
                Debug.Log("3c");

                // メッシュを生成
                Mesh mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.triangles = indices;

                var uv = new Vector2[len];
                mesh.uv = uv;

                mesh.RecalculateNormals();
                var filter = roadX.GetComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                
                var collider = roadX.GetComponent<MeshCollider>();
                collider.sharedMesh  = mesh;

                string saveMeshdirPath = @"Assets\Resources\Mesh\"+currentMapIndex;
                if (!Directory.Exists(saveMeshdirPath)) Directory.CreateDirectory(saveMeshdirPath);
                if (saveMeshAsAsset){
                    string saveMeshFilename = saveMeshdirPath+@"\road-mesh-"+roadname+".asset";
                    //Debug.Log("saveMeshFilename "+saveMeshFilename);
                    // UnityEditor.AssetDatabase.CreateAsset(filter.sharedMesh,saveMeshFilename );      
                    Mesh assetM2 = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(saveMeshFilename);
                    if( assetM2 == null){
                        UnityEditor.AssetDatabase.CreateAsset(filter.sharedMesh,saveMeshFilename );
                    } else {
                        filter.sharedMesh = assetM2;
                    }
                }


                MeshRenderer meshRenderer = roadX.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = roadMaterial;
                //Debug.Log("4");

                float elapsed = (float)sw2.Elapsed.TotalSeconds;                 
                if (elapsed>0.3) {
                    sw2.Stop(); //計測終了
                    sw2.Restart();
                    sw2.Start();
                   yield return null;  
                }  
                //    Debug.Log("5");
                //    yield return new WaitForSecondsRealtime(0.01f);  
            
                //    Debug.Log("6");

                                                                    SceneView.RepaintAll(); // シーンビュー更新  
                            UnityEditor.AssetDatabase.SaveAssets();
                            UnityEditor.AssetDatabase.Refresh();   
                // yield return null;  
                                                    SceneView.RepaintAll(); // シーンビュー更新  
                            UnityEditor.AssetDatabase.SaveAssets();
                            UnityEditor.AssetDatabase.Refresh();                               

                index1++;
                Debug.Log("1");
            }
            Debug.Log("2");
        }
        cordataTRAN.cor1finished = true;
        sw.Stop(); //計測終了
        sw2.Stop(); //計測終了 
        //Debug.Log("Exit from createRoadFromSurfaces");
    }



    // gmlファイルからLOD3のTRANを生成
    private void CreateRoadLOD3(string currentMapIndex, GameObject goCurrent, Position lowerCorner, Position upperCorner, float maxheight,string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;
        GameObject bldg = new GameObject("TranLOD3");
        bldg.transform.parent = goCurrent.transform;
        CityGMLParser cgp = new CityGMLParser();
        cgp.setPositions(lowerCorner, upperCorner);

        List<Building> buildings = new List<Building>();
        cordataTRANLOD3.cor1finished = cordataTRANLOD3.cor2finished = false;        
        cordataTRANLOD3.coroutine1 = EditorCoroutineUtility.StartCoroutine(cgp.GetTRANLOD3s( gmlPath, buildings,cordataTRANLOD3), this);  
        cordataTRANLOD3.coroutine2 = EditorCoroutineUtility.StartCoroutine(makeTRANLOD3Model( bldg, buildings,cordataTRANLOD3,currentMapIndex), this);  
    }
    public IEnumerator makeTRANLOD3Model( GameObject bldg, List<Building> buildings, CorData cordata,string currentMapIndex){
        int iy = 0;
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start(); //計測開始
        var sw2 = new System.Diagnostics.Stopwatch();
        sw2.Start(); //計測開始         
        while ( !cordata.cor1finished) {
            iy++;
            if (iy%100==0) {
                yield return null;  
            }
        }
        var trans = buildings.ToArray();
        for (int i = 0; i < trans.Length; i++)
        {
            Debug.Log(i+"/"+trans.Length);
            if (i <301) {
              // continue;
            }
            
            Building b = trans[i];
//            UnityModelGenerator mg = new UnityModelGenerator(b, baseLowerCorner, roadUseCollider, false, null);// bldgUseTexture, bldgMaterial);
//            UnityModelGenerator mg = ScriptableObject.CreateInstance<UnityModelGenerator>();
            mg.ModelInitialize(b, baseLowerCorner, roadUseCollider, false, null,bldgTexsize,saveMeshAsAsset);
            mg.Create(bldg,useAVGPosition,currentMapIndex,i);
            if (i > 601) {
               //break;
            }
            //Resources.UnloadUnusedAssets();
            iy++;
            if (iy%100==0) {
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
        cordata.cor2finished = true;
        sw.Stop(); //計測終了
        sw2.Stop(); //計測終了                
    }



    IEnumerator CreateBLDGLOD3(string currentMapIndex, Position lowerCorner, Position upperCorner, GameObject goBldgLOD3){
                string bldgfilename = getPath(udxpath, currentMapIndex, "bldg");
                // Debug.Log("bldg "+bldgfilename);  
                if (bldgON && bldgLOD3 && bldgfilename != null && File.Exists(bldgfilename)) 
                {
                    Debug.Log("start "+bldgfilename);  
                    GameObject goCurrentBldgLOD3 = new GameObject(currentMapIndex);
                    goCurrentBldgLOD3.transform.parent = goBldgLOD3.transform;

                    currentcreating = currentMapIndex+" BLDGLOD3";
                    makeBLDGLOD3(currentMapIndex, goCurrentBldgLOD3, lowerCorner, upperCorner,maxheight,bldgfilename);
                    int iyd = 0;
                    while ( !cordataBLDGLOD3.cor2finished) {
                        iyd++;
                        if (iyd%100==0) {
                            yield return null;  
                        }
                    }      
                    cordataBLDGLOD3.cor1finished = false;
                    cordataBLDGLOD3.cor2finished = false;                       
                // } else {
                //     Debug.Log("LOD3 not found");
                }
        cordataBLDGLOD3.cor3finished = true;  
    }



    // gmlファイルから建物を生成
    private void makeBLDGLOD3(string currentMapIndex, GameObject goCurrent, Position lowerCorner, Position upperCorner, float maxheight,string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;
        // GameObject bldg = new GameObject("BldgLOD3");
        // bldg.transform.parent = goCurrent.transform;

        CityGMLParser cgp = new CityGMLParser(lowerCorner, upperCorner);

        List<Building> buildings = new List<Building>();
        cordataBLDGLOD3.cor1finished = cordataBLDGLOD3.cor2finished = false;
        cordataBLDGLOD3.coroutine1 = EditorCoroutineUtility.StartCoroutine(cgp.GetBuildingsLOD3( gmlPath, buildings,cordataBLDGLOD3), this);  
        cordataBLDGLOD3.coroutine2 = EditorCoroutineUtility.StartCoroutine(makeBLDGModel( goCurrent, buildings,cordataBLDGLOD3,currentMapIndex,bldgLOD3UseTexture,bldgLOD3UseColor), this);  
    }    


    IEnumerator CreateBLDGLOD2(string currentMapIndex, Position lowerCorner, Position upperCorner, GameObject goBldgLOD2){
                string bldgfilename = getPath(udxpath, currentMapIndex, "bldg");
                // Debug.Log("bldg "+bldgfilename);  

                if (bldgON && bldgLOD2 && bldgfilename != null && File.Exists(bldgfilename)) 
                {
                    Debug.Log("start "+bldgfilename);  

                    GameObject goCurrentBldgLOD2 = new GameObject(currentMapIndex);
                    goCurrentBldgLOD2.transform.parent = goBldgLOD2.transform;

                    currentcreating = currentMapIndex+" BLDGLOD2";
                    makeBLDGLOD2(currentMapIndex, goCurrentBldgLOD2, lowerCorner, upperCorner,maxheight,bldgfilename);
                    int iyd = 0;
                    while ( !cordataBLDG.cor2finished) {
                        iyd++;
                        if (iyd%100==0) {
                            yield return null;  
                        }
                    }      
                    cordataBLDG.cor1finished = false;
                    cordataBLDG.cor2finished = false;   
                // } else {
                //     Debug.Log("LOD2 not found");                                        
                }
        cordataBLDG.cor3finished = true;                  
    }



    private void makeBLDGLOD2(string currentMapIndex, GameObject goCurrent, Position lowerCorner, Position upperCorner, float maxheight,string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;
        // GameObject bldg = new GameObject("Bldg");
        // bldg.transform.parent = goCurrent.transform;

        CityGMLParser cgp = new CityGMLParser(lowerCorner, upperCorner);

        List<Building> buildings = new List<Building>();
        cordataBLDG.cor1finished = cordataBLDG.cor2finished = false;
        //Debug.Log("StartCoroutine "+cordataBLDG.cor1finished+" "+cordataBLDG.cor2finished);         
        cordataBLDG.coroutine1 = EditorCoroutineUtility.StartCoroutine(cgp.GetBuildings( gmlPath, buildings,cordataBLDG), this);  
        cordataBLDG.coroutine2 = EditorCoroutineUtility.StartCoroutine(makeBLDGModel( goCurrent, buildings,cordataBLDG,currentMapIndex,bldgLOD2UseTexture,bldgLOD2UseColor), this);  
    }
    public IEnumerator makeBLDGModel( GameObject bldg, List<Building> buildings, CorData cordata,string currentMapIndex, bool bldgUseTexture, bool useColor){
        //Debug.Log("Create Wait "+cordata.cor1finished);  
        int iyd = 0;
        float prevTime = Time.time;
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start(); //計測開始       
        var sw2 = new System.Diagnostics.Stopwatch();
        sw2.Start(); //計測開始   
        while ( !cordata.cor1finished) {
            iyd++;
            if (iyd%100==0) {
                yield return null;  
            }
        }  
        //Debug.Log("Create Start");  
        var trans = buildings.ToArray();
        for (int i = 0; i < trans.Length; i++)
        {
            Debug.Log(i+"/"+trans.Length);
            Building b = buildings[i];
            mg.ModelInitialize(b, baseLowerCorner, bldgUseCollider, bldgUseTexture, bldgMaterial,bldgTexsize,saveMeshAsAsset,useColor,bldgcolors,bldgMaxColor);
            mg.Create(bldg,useAVGPosition,currentMapIndex,i);
            iyd++;
            //Debug.Log("Time.time "+ Time.time+" sw.Elapsed "+ sw.Elapsed+" sw2.Elapsed "+ sw2.Elapsed); //経過時間);        

            float elapsed = (float)sw2.Elapsed.TotalSeconds;   
            if (elapsed>0.3) {
                sw2.Stop(); //計測終了
                sw2.Restart();                
                sw2.Start();
                yield return null;  
            }
            if (iyd % 100 == 0) {
                yield return null;  
            }                    

        }
        sw2.Stop(); //計測終了
        sw.Stop(); //計測終了
        cordata.cor2finished = true;
    }



    IEnumerator CreateFRN(string currentMapIndex, Position lowerCorner, Position upperCorner, GameObject goFrn){
                string frnfilename = getPath(udxpath, currentMapIndex, "frn");
                Debug.Log("frn "+frnfilename);                 
                if (frnON && frnfilename != null && File.Exists(frnfilename)) 
                {
                    GameObject goCurrentFrn = new GameObject(currentMapIndex);
                    goCurrentFrn.transform.parent = goFrn.transform;

                    currentcreating = currentMapIndex+" FRN";
                    makeFRN(currentMapIndex, goCurrentFrn, lowerCorner, upperCorner,maxheight,frnfilename);
                    int iyd = 0;
                    while ( !cordataFRN.cor2finished) {
                        iyd++;
                        if (iyd%100==0) {
                            yield return null;  
                        }
                    }      
                    cordataFRN.cor1finished = false;
                    cordataFRN.cor2finished = false;
                }        
                cordataFRN.cor3finished = true;
    }

    // gmlファイルからfrnを生成
    private void makeFRN(string currentMapIndex, GameObject goCurrent, Position lowerCorner, Position upperCorner, float maxheight,string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;
        GameObject bldg = new GameObject("Frn");
        bldg.transform.parent = goCurrent.transform;
        //bldg.transform.position = new Vector3(0,frnOffsetY,0);
        CityGMLParser cgp = new CityGMLParser(lowerCorner, upperCorner);

        //var frns = cgp.GetFRNs( gmlPath);
        List<Building> buildings = new List<Building>();
        cordataFRN.cor1finished = cordataFRN.cor2finished = false;
        cordataFRN.coroutine1 = EditorCoroutineUtility.StartCoroutine(cgp.GetFRNs( gmlPath, buildings,cordataFRN,frnSplit), this);  
        cordataFRN.coroutine2 = EditorCoroutineUtility.StartCoroutine(makeFRNModel( bldg, buildings,cordataFRN,currentMapIndex), this);          
        // return bldg;
    }
    public IEnumerator makeFRNModel( GameObject bldg, List<Building> buildings, CorData cordata,string currentMapIndex){   
        int iyd = 0;
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start(); //計測開始
        var sw2 = new System.Diagnostics.Stopwatch();
        sw2.Start(); //計測開始        
        while ( !cordata.cor1finished) {
            iyd++;
            if (iyd%100==0) {
                yield return null;  
            }
        }    
        var frns = buildings.ToArray();               
        for (int i = 0; i < frns.Length; i++)
        {
            Debug.Log(i+"/"+frns.Length);
            if (i <701) {
              //continue;
            }
            Building b = frns[i];
//            UnityModelGenerator mg = new UnityModelGenerator(b, baseLowerCorner, frnUseCollider, frnUseTexture, frnMaterial);
//            UnityModelGenerator mg = ScriptableObject.CreateInstance<UnityModelGenerator>();
            mg.ModelInitialize(b, baseLowerCorner, frnUseCollider, frnUseTexture, null,frnTexsize,saveMeshAsAsset);
            mg.Create(bldg,useAVGPosition,currentMapIndex,i);
            if (i > 401) {
               //break;
            }
            iyd++;
            //Debug.Log("Time.time "+ Time.time+" sw.Elapsed "+ sw.Elapsed+" sw2.Elapsed "+ sw2.Elapsed); //経過時間);        
            float elapsed = (float)sw2.Elapsed.TotalSeconds;   
//            if (elapsed>0.3) {
            if (elapsed>1) {
                sw2.Stop(); //計測終了
                sw2.Restart();
                sw2.Start();
                yield return null;  
            }             
            Resources.UnloadUnusedAssets();
        }
        cordata.cor2finished = true;
        sw.Stop(); //計測終了
        sw2.Stop(); //計測終了        
    }



    // void CreateVEG(string currentMapIndex, Position lowerCorner, Position upperCorner, GameObject goVeg){
    //     CreateVEGSub
    // }
    IEnumerator CreateVEG(string currentMapIndex, Position lowerCorner, Position upperCorner, GameObject goVeg){
                string vegfilename = getPath(udxpath, currentMapIndex, "veg");
                Debug.Log("veg "+vegfilename);  
                if (vegON && vegfilename != null && File.Exists(vegfilename)) 
                {
                    GameObject goCurrentVeg = new GameObject(currentMapIndex);
                    goCurrentVeg.transform.parent = goVeg.transform;

                    currentcreating = currentMapIndex+" VEG";
                    makeVEG(currentMapIndex, goCurrentVeg, lowerCorner, upperCorner,maxheight,vegfilename);
                    int iyd = 0;
                    while ( !cordataVEG.cor2finished) {
                        iyd++;
                        if (iyd%100==0) {
                            yield return null;  
                        }
                    }      
                    cordataVEG.cor1finished = false;
                    cordataVEG.cor2finished = false;                             
                }     

                cordataVEG.cor3finished = true;   
    }


    // gmlファイルからVEGを生成
    private void makeVEG(string currentMapIndex, GameObject goCurrent, Position lowerCorner, Position upperCorner, float maxheight,string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;
        GameObject bldg = new GameObject("Veg");
        bldg.transform.parent = goCurrent.transform;

        CityGMLParser cgp = new CityGMLParser(lowerCorner, upperCorner);

        List<Building> buildings = new List<Building>();
        cordataVEG.cor1finished = cordataVEG.cor2finished = false;
        cordataVEG.coroutine1 = EditorCoroutineUtility.StartCoroutine(cgp.GetVEGs( gmlPath, buildings,cordataVEG), this);  
        cordataVEG.coroutine2 = EditorCoroutineUtility.StartCoroutine(makeVEGModel( bldg, buildings,cordataVEG,currentMapIndex), this);  
    }
    public IEnumerator makeVEGModel( GameObject bldg, List<Building> buildings, CorData cordata,string currentMapIndex){    
        int iyd = 0;
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start(); //計測開始
        var sw2 = new System.Diagnostics.Stopwatch();
        sw2.Start(); //計測開始        
        while ( !cordata.cor1finished) {
            iyd++;
            if (iyd%100==0) {
                yield return null;  
            }
        }                
        var vegs = buildings.ToArray();
        for (int i = 0; i < vegs.Length; i++)
        {
            Debug.Log(i+"/"+vegs.Length);

            if (i <301) {
              // continue;
            }
            Building b = vegs[i];
//            UnityModelGenerator mg = new UnityModelGenerator(b, baseLowerCorner, vegUseCollider, vegUseTexture, vegMaterial);
//            UnityModelGenerator mg = ScriptableObject.CreateInstance<UnityModelGenerator>();
            mg.ModelInitialize(b, baseLowerCorner, vegUseCollider, false, null,bldgTexsize,saveMeshAsAsset);
            mg.bldgMaterial = vegPlantCoverMaterial;
            mg.prefab = vegTreePrefab;
            mg.height = vegTreePrefabHeight;
            mg.vegTreePrefabNumber = vegTreePrefabNumber;
            mg.Create(bldg,useAVGPosition,currentMapIndex,i);
            if (i > 601) {
               //break;
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
            Resources.UnloadUnusedAssets();
        }
        cordata.cor2finished = true;
        sw.Stop(); //計測終了
        sw2.Stop(); //計測終了          
    }











    string [] bldgcolors;
    // 色の16進数の文字列の配列を取得
    string []  GetBldgColors() {
        string colordirPath = @"Assets\Resources\ColorBLDG";
        string [] tmp = {"FFFFFF"};
        if (!Directory.Exists(colordirPath))
        { 
            return tmp;        
        }

        string[] files = Directory.GetFiles(colordirPath, "*.mat", SearchOption.AllDirectories);
        List<string>tmplist = new List<string>();
        for (int i = 0; i < files.Length;i++){
            string name = Path.GetFileNameWithoutExtension(files[i]);
//            Debug.Log(files[i]+" "+name);
            if (name.Length == 6) {
                tmplist.Add(name);
            }
        }
        return tmplist.ToArray();

    }

    // 地形を生成
    IEnumerator CreateDemAll8(GameObject go){
        // GameObject go = new GameObject(name);
        // go.transform.parent = goPLATEAU.transform;
                    int iyd = 0;
        for(int z = 0; z < zsize; z++) 
        {
            for(int x = 0; x < xsize; x++) 
            {
                        iyd++;
                        if (iyd%100==0) {
                            yield return null;  
                        }                
                GridSquareMeshCode mi = mizero.add3(z,x);
                string currentMapIndex = mi.index;
                currentcreating = currentMapIndex;
                string demfilename = getPath(udxpath, currentMapIndex, "dem");

                if (demfilename == null ){
                     continue;
                }
                Debug.Log("dem8 "+demfilename);
                var corners = getCorner(demfilename);
                
                GameObject goCurrent = new GameObject(currentMapIndex);
                goCurrent.transform.parent = go.transform;

                // 右上の位置
                GameObject goupper   = new GameObject("upper");//GameObject.CreatePrimitive (PrimitiveType.Sphere);
                Vector3 v3u = corners.upperCorner.ToVector3(baseLowerCorner);
                if (v3u.y > maxheight) {
                    maxheight = v3u.y;
                }
                goupper.transform.position = v3u;
                //goupper.name = "upper";

                // 左下の位置
                goupper.transform.parent = goCurrent.transform;
                GameObject golower   = new GameObject("lower");///GameObject.CreatePrimitive (PrimitiveType.Sphere);
                Vector3 v3l = corners.lowerCorner.ToVector3(baseLowerCorner);
                golower.transform.position = v3l;
                //golower.name = "lower";
                golower.transform.parent = goCurrent.transform;
                // Debug.Log(corners.lowerCorner);
                if (demfilename != null && File.Exists(demfilename)) 
                {
                    cordataDEM.cor1finished = false;
                    cordataDEM.coroutine1 = EditorCoroutineUtility.StartCoroutine(makeDEM8(currentMapIndex, goCurrent, corners.lowerCorner, corners.upperCorner, demfilename),this) ;            
                    while(!cordataDEM.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }

                    
                    // makeDEM8(currentMapIndex, goCurrent, corners.lowerCorner, corners.upperCorner, demfilename);
                }
            }
        }
        // return go;
        cordataDEM.cor3finished = true;
    }

    // 地形を生成
    IEnumerator CreateDemAll6(GameObject go){
                    int iyd = 0;        
        Position lowerCorner, upperCorner;
        Debug.Log("CreateDemAll6 Start");                

        // GameObject go = new GameObject(name);
        // go.transform.parent = goPLATEAUmakeDEM8(.transform;
//        GridSquareMeshCode mi6upperBlock = mizero.add3(zsize-1,xsize-1); // ３次メッシュでの右上位置ブロックのコードを取得
        GridSquareMeshCode mi6upperBlock = mizero.add3(zsize-1,xsize-1); // ３次メッシュでの右上位置ブロックのコードを取得
        Debug.Log("mi6upperBlock.index: "+ mi6upperBlock.index);
        GridSquareMeshCode mizero3 = new GridSquareMeshCode(mizero.index2); // ３次メッシュでの右上位置ブロックのコードを取得
        GridSquareMeshCode mi6upperBlock3 = new GridSquareMeshCode(mi6upperBlock.index2);
        Debug.Log("mizero3.index: " + mizero3.index+" mizero3.index2: "+mizero3.index2);
        Debug.Log("mi6upperBlock3.index: "+ mi6upperBlock3.index+" mi6upperBlock3.index2: "+mi6upperBlock3.index2);
        (int zsize6, int xsize6) = mi6upperBlock3.diff2(mizero3);// ２次メッシュでのブロック数を取得
        Debug.Log("diff xsize6: "+xsize6+" zsize6:"+zsize6);

        for(int z = 0; z <= zsize6; z++) 
        {
            for(int x = 0; x <= xsize6; x++) 
            {
                        iyd++;
                        if (iyd%100==0) {
                            yield return null;  
                        }                  
                GridSquareMeshCode mi = mizero.add2(z,x);

                // GridSquareMeshCode miL = new GridSquareMeshCode(mizero.index2);
                GridSquareMeshCode miL = new GridSquareMeshCode(mi.index2);
                GridSquareMeshCode miU = miL.add2(1,1);
                string currentMapIndex = mi.index2;
                currentcreating = currentMapIndex;
                Debug.Log("CreateDemAll6 currentMapIndex "+currentMapIndex);                

                GameObject goCurrent = new GameObject(currentMapIndex);
                goCurrent.transform.parent = go.transform;
                lowerCorner = new Position(miL.lat, miL.lon,0); //２次メッシュでのブロック全体の左下
                upperCorner = new Position(miU.lat, miU.lon,0); //２次メッシュでのブロック全体の右上  

                cordataDEM.cor1finished = false;
                cordataDEM.coroutine1 = EditorCoroutineUtility.StartCoroutine(makeDEM6(currentMapIndex, goCurrent, lowerCorner, upperCorner),this) ;            
                while(!cordataDEM.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }



                // makeDEM6(currentMapIndex, goCurrent, lowerCorner, upperCorner);

            }
        }
        // return go;
        cordataDEM.cor3finished = true;   
    }

    GameObject goPLATEAU;
    float maxheight = 0;


    CorData cordataall= new CorData();
    CorData cordataTRAN= new CorData();
    CorData cordataTRANLOD3= new CorData();
    CorData cordataBLDG= new CorData();    
    CorData cordataBLDGLOD3= new CorData();    
    CorData cordataFRN= new CorData();  
    CorData cordataVEG= new CorData();   

    CorData cordataRoad8 = new CorData();
    CorData cordataRoad6 = new CorData();

    CorData cordataDEM = new CorData();

    CorData cordataImage= new CorData();


    public EditorCoroutine coroutine; 










    [ContextMenu("Create")]
    private void makeAllstart()
    {
        AssetDatabase.StartAssetEditing();
        coroutine = EditorCoroutineUtility.StartCoroutine(makeAll(), this);  
        AssetDatabase.StopAssetEditing();
        // AssetDatabase.Refresh();
    }
    
    int roadtype = 8;
    int demtype = 8;
    public IEnumerator makeAll()
    {
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start(); //計測開始

        mg = ScriptableObject.CreateInstance<UnityModelGenerator>();



        bldgcolors = GetBldgColors();
        // for(int i = 0; i < bldgcolors.Length;i++) {
        //     Debug.Log("Color "+bldgcolors[i]);
        // }

        int iyd = 0;
        //getImage(mapindex,226233,105038);


        string baseMapIndex = basemapindex;

        // basemapindexから基準の緯度経度を決定高さは0基準に
        GridSquareMeshCode mibase = new GridSquareMeshCode(baseMapIndex);
        Debug.Log("BASE "+mibase.lat+" "+mibase.lon);
        baseLowerCorner = new Position(mibase.lat,mibase.lon,0);
        mizero = new GridSquareMeshCode(zeromapindex);
        mimax  = mizero.add3(zsize,xsize);
        Debug.Log(mizero.index+" "+mimax.index);
        pzero = new Position(mizero.lat,mizero.lon,0);
        pmax  = new Position(mimax.lat,mimax.lon,0);
        v3zero = pzero.ToVector3(baseLowerCorner);
        v3max  = pmax.ToVector3(baseLowerCorner);
        Debug.Log(v3zero);
        Debug.Log(v3max);

        
        string last = udxpath.Substring(udxpath.Length-1);
        // Debug.Log(last);
        if (last !=@"\") {
            udxpath +=@"\";
        }
        string basedemfilename = getPath(udxpath, baseMapIndex, "dem");
        if (basedemfilename == null) {
            basedemfilename = getPath6(udxpath, baseMapIndex, "dem");
            if (basedemfilename != null) {
                demtype = 6;
            }
        }        
        //baseLowerCorner = getBaseLowerCorner(basedemfilename);

        string basetranfilename = getPath(udxpath, baseMapIndex, "tran");
        if (basetranfilename == null) {
            basetranfilename = getPath6(udxpath, baseMapIndex, "tran");
            if (basetranfilename != null) {
                roadtype = 6;
            }
        }
        // Debug.Log("roadtype:"+roadtype);
        goPLATEAU = new GameObject("PLATEAU");
        goPLATEAU.transform.parent = transform;
        GameObject goTranLOD3 = null;
        GameObject goTranLOD2 = null;
        GameObject goBldgLOD3 = null;
        GameObject goBldgLOD2 = null;
        GameObject goFrn = null;
        GameObject goVeg = null;
        if (roadON && roadLOD3){ 
            goTranLOD3 = new GameObject("TranLOD3");
            goTranLOD3.transform.parent = goPLATEAU.transform;        
        }
        if (roadON && roadLOD1){ 
            goTranLOD2 = new GameObject("TranLOD2");
            goTranLOD2.transform.parent = goPLATEAU.transform;        
        }
        if (bldgON && bldgLOD3){ 
            goBldgLOD3 = new GameObject("BldgLOD3");
            goBldgLOD3.transform.parent = goPLATEAU.transform;        
        }
        if (bldgON && bldgLOD2){ 
            goBldgLOD2 = new GameObject("BldgLOD2");
            goBldgLOD2.transform.parent = goPLATEAU.transform;       
        }
        if (frnON){ 
            goFrn = new GameObject("Frn");
            goFrn.transform.parent = goPLATEAU.transform;    
            goFrn.transform.position = new Vector3(0,frnOffsetY,0);
    
        }
        if (vegON){ 
            goVeg = new GameObject("Veg");
            goVeg.transform.parent = goPLATEAU.transform;        
        }
        string currentMapIndex = baseMapIndex;



        if (roadLOD1Array){
            heightArray = new float[heightarraysize*xsize, heightarraysize*zsize];
            roadArray = new int[heightarraysize*xsize, heightarraysize*zsize];
            heightSumArray = new float[heightarraysize*xsize, heightarraysize*zsize];
            heightCountArray = new int[heightarraysize*xsize, heightarraysize*zsize];
            heightAverageArray = new float[heightarraysize*xsize, heightarraysize*zsize];
        }
        // 道路 LOD3
        if (roadON && roadLOD3){
            for(int z = 0; z < zsize; z++) 
            {
                for(int x = 0; x < xsize; x++) 
                {
                    GridSquareMeshCode mi = mizero.add3(z,x);
                    currentMapIndex = mi.index;
                    Debug.Log("z: "+ z+" x:"+x);
                    Debug.Log(currentMapIndex);
                    currentcreating = currentMapIndex;
                    GameObject goCurrentTranLOD3 = new GameObject(currentMapIndex);
                    goCurrentTranLOD3.transform.parent = goTranLOD3.transform;

//                     string demfilename = getPath(udxpath, currentMapIndex, "dem");
// //                    var corners = getCorner(demfilename);
                    var corners =  getCornerFromIndex3(currentMapIndex);

                    string tranfilename = getPath(udxpath, currentMapIndex, "tran");
                    Debug.Log(tranfilename);
                    if (roadON && roadLOD3 && tranfilename != null && File.Exists(tranfilename))
                    {
                        currentcreating = currentMapIndex+" TRANLOD3";                
                        CreateRoadLOD3(currentMapIndex, goCurrentTranLOD3, corners.lowerCorner, corners.upperCorner,maxheight, tranfilename);
                        while ( !cordataTRANLOD3.cor2finished) {
                            iyd++;
                            if (iyd%100==0) {
                                yield return null;  
                            }
                        }      
                        cordataTRANLOD3.cor1finished = false;
                        cordataTRANLOD3.cor2finished = false;                     

                    } 
                }
            }
        }

        if (demON && roadON && roadLOD1 && roadLOD1Array) {
            Debug.Log("demON && roadON && roadLOD1 && roadLOD1Array");
            // 1 仮地面生成 配列で道路の場合はtmpDem
            //if (false)
            GameObject tmpDem = null;
            string name = "tmpDem"; //(!roadLOD1Array) ? "Dem" : "tmpDem";
            tmpDem = new GameObject(name);       
            tmpDem.transform.parent = goPLATEAU.transform;
    
            if (demtype == 8) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll8(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }
            } else if (demtype == 6) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll6(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }
            }    
            // 2 高さ配列
            makeHeightArray(); // 高さの配列を作成
            // 3 仮地面削除
            DestroyImmediate(tmpDem); // 仮の地面を削除
            tmpDem = null;                            
            // 4 仮道路生成（高さ）
            GameObject tmpTrn = new GameObject("tmpTrn");       
            tmpTrn.transform.parent = goPLATEAU.transform;
            if (roadtype == 8) {
                cordataRoad8.cor1finished = false;
                cordataRoad8.coroutine1 = EditorCoroutineUtility.StartCoroutine(CreateRoad8(tmpTrn,false),this);
                while(!cordataRoad8.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }
            } else if (roadtype == 6) {
                cordataRoad6.cor1finished = false;
                cordataRoad6.coroutine1 = EditorCoroutineUtility.StartCoroutine(CreateRoad6(tmpTrn,false),this);
                while(!cordataRoad6.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }
            }    
            // 5 道路配列
            makeRoadArray(); // 道路の配列を作成
            // 6 仮道路削除
            DestroyImmediate(tmpTrn); // 仮の地面を削除
            tmpTrn = null;                            
            // 7 平均化. 道路のある位置の高さを平均化
            getAverage();   

            // 8 道路生成（平均）
            if (roadtype == 8) {
                cordataRoad8.cor1finished = false;
                cordataRoad8.coroutine1 = EditorCoroutineUtility.StartCoroutine(CreateRoad8(goTranLOD2,true),this);
                while(!cordataRoad8.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }
            } else if (roadtype == 6) {
                cordataRoad6.cor1finished = false;
                cordataRoad6.coroutine1 = EditorCoroutineUtility.StartCoroutine(CreateRoad6(goTranLOD2,true),this);
                while(!cordataRoad6.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }
            }                

            // 9 地面生成
            tmpDem = new GameObject("Dem");   
            tmpDem.transform.parent = goPLATEAU.transform;
            if (demtype == 8) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll8(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }


            } else if (demtype == 6) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll6(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }
            }             
   


        } else if (demON && roadON &&  roadLOD1) {
            Debug.Log("demON && roadON && roadLOD1 && !         roadLOD1Array");
            // 1 仮地面生成 配列で道路の場合はtmpDem
            //if (false)
            GameObject tmpDem = null;
            string name = "tmpDem"; //(!roadLOD1Array) ? "Dem" : "tmpDem";
            tmpDem = new GameObject(name);       
            tmpDem.transform.parent = goPLATEAU.transform;
    
            if (demtype == 8) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll8(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }
            } else if (demtype == 6) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll6(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }
            }   
            // 2 道路生成（高さ）
            if (roadtype == 8) {
                cordataRoad8.cor1finished = false;
                cordataRoad8.coroutine1 = EditorCoroutineUtility.StartCoroutine(CreateRoad8(goTranLOD2,false),this);
                while(!cordataRoad8.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }
            } else if (roadtype == 6) {
                cordataRoad6.cor1finished = false;
                cordataRoad6.coroutine1 = EditorCoroutineUtility.StartCoroutine(CreateRoad6(goTranLOD2,false),this);
                while(!cordataRoad6.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }
            }   
            // 3 仮地面削除
            DestroyImmediate(tmpDem); // 仮の地面を削除
            tmpDem = null;                        
            // 4 地面生成
            tmpDem = new GameObject("Dem");   
            tmpDem.transform.parent = goPLATEAU.transform;
            if (demtype == 8) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll8(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }


            } else if (demtype == 6) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll6(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }
            }  

        } else if (demON) {
            GameObject tmpDem = null;
            string name = "Dem"; //(!roadLOD1Array) ? "Dem" : "tmpDem";
            tmpDem = new GameObject(name);       
            tmpDem.transform.parent = goPLATEAU.transform;
    
            if (demtype == 8) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll8(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }
            } else if (demtype == 6) {
                cordataDEM.cor1finished = false;
                cordataDEM.cor2finished = false;        
                cordataDEM.cor3finished = false;
                cordataDEM.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateDemAll6(tmpDem),this) ;            
                while(!cordataDEM.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }
            } 
        } else if (roadON && roadLOD1) {
            // 道路生成（高さ）
            if (roadtype == 8) {
                cordataRoad8.cor1finished = false;
                cordataRoad8.coroutine1 = EditorCoroutineUtility.StartCoroutine(CreateRoad8(goTranLOD2,false),this);
                while(!cordataRoad8.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }
            } else if (roadtype == 6) {
                cordataRoad6.cor1finished = false;
                cordataRoad6.coroutine1 = EditorCoroutineUtility.StartCoroutine(CreateRoad6(goTranLOD2,false),this);
                while(!cordataRoad6.cor1finished){yield return new WaitForSecondsRealtime(0.01f); }
            }   
        }

        ////////////////////////////////////////////////////////////////////////
        Debug.Log("after dem");
        for(int z = 0; z < zsize; z++) 
        {
            for(int x = 0; x < xsize; x++) 
            {
                GridSquareMeshCode mi = mizero.add3(z,x);
                currentMapIndex = mi.index;
                currentcreating = currentMapIndex;
                // string demfilename = getPath(udxpath, currentMapIndex, "dem");
                // string tranfilename = getPath(udxpath, currentMapIndex, "tran");

                Debug.Log("MeshCode "+currentMapIndex);
                GridSquareMeshCode miL = new GridSquareMeshCode(currentMapIndex);
                GridSquareMeshCode miU = miL.add3(1,1);
                Position lowerCorner = new Position(miL.lat, miL.lon,0);
                Position upperCorner = new Position(miU.lat, miU.lon,0);
                //var corners = getCorner(demfilename);

                // UnityEditor.AssetDatabase.SaveAssets();
                //UnityEditor.AssetDatabase.Refresh(); 
                // EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll(); // シーンビュー更新

                // LOD3の建物を生成
                cordataBLDGLOD3.cor3finished = false;
                cordataBLDGLOD3.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateBLDGLOD3(currentMapIndex, lowerCorner, upperCorner, goBldgLOD3),this);
                while(!cordataBLDGLOD3.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }

                // LOD2の建物を生成
                cordataBLDG.cor3finished = false;
                cordataBLDG.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateBLDGLOD2(currentMapIndex, lowerCorner, upperCorner, goBldgLOD2),this);
                while(!cordataBLDG.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }

                // 都市設備を生成
                cordataFRN.cor3finished = false;
                cordataFRN.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateFRN(currentMapIndex, lowerCorner, upperCorner, goFrn),this);
                while(!cordataFRN.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }

                // 植生を生成
                cordataVEG.cor3finished = false;
                cordataVEG.coroutine3 = EditorCoroutineUtility.StartCoroutine(CreateVEG(currentMapIndex, lowerCorner, upperCorner, goVeg),this) ;            
                while(!cordataVEG.cor3finished){yield return new WaitForSecondsRealtime(0.01f); }



            }
        }
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        sw.Stop(); //計測終了
        Debug.Log(" Finished "+ sw.Elapsed );
        currentcreating = " Finished "+sw.Elapsed; 
    }
    
    
/*
private EditorCoroutine _coroutine;  
    private static IEnumerator CountUpCoroutine()  
    {
        var count = 0;  
        while (true)  
        {
            Debug.Log(count++.ToString());  
            yield return null;  
        }
    }

    [ContextMenu("Create ")]
    private void MakeAll()
    {
        _coroutine = EditorCoroutineUtility.StartCoroutine(CountUpCoroutine(), this);  

    }
    */
    [ContextMenu("Stop ")]
    private void Stop()
    {
        if (coroutine!= null) EditorCoroutineUtility.StopCoroutine( coroutine);    
        if (cordataTRAN.coroutine1!= null) EditorCoroutineUtility.StopCoroutine( cordataTRAN.coroutine1);  
        if (cordataTRANLOD3.coroutine1!= null) EditorCoroutineUtility.StopCoroutine( cordataTRANLOD3.coroutine1);  
        if (cordataTRANLOD3.coroutine2!= null) EditorCoroutineUtility.StopCoroutine( cordataTRANLOD3.coroutine2);  

        if (cordataDEM.coroutine1!= null) EditorCoroutineUtility.StopCoroutine( cordataDEM.coroutine1);  
        if (cordataDEM.coroutine2!= null) EditorCoroutineUtility.StopCoroutine( cordataDEM.coroutine2);  
        if (cordataDEM.coroutine3!= null) EditorCoroutineUtility.StopCoroutine( cordataDEM.coroutine3);  


        if (cordataBLDG.coroutine1!= null) EditorCoroutineUtility.StopCoroutine( cordataBLDG.coroutine1);  
        if (cordataBLDG.coroutine2!= null) EditorCoroutineUtility.StopCoroutine( cordataBLDG.coroutine2);  
        if (cordataBLDG.coroutine3!= null) EditorCoroutineUtility.StopCoroutine( cordataBLDG.coroutine3);  

        if (cordataBLDGLOD3.coroutine1!= null) EditorCoroutineUtility.StopCoroutine( cordataBLDGLOD3.coroutine1);  
        if (cordataBLDGLOD3.coroutine2!= null) EditorCoroutineUtility.StopCoroutine( cordataBLDGLOD3.coroutine2);  
        if (cordataBLDGLOD3.coroutine3!= null) EditorCoroutineUtility.StopCoroutine( cordataBLDGLOD3.coroutine3);  

        if (cordataFRN.coroutine1!= null) EditorCoroutineUtility.StopCoroutine( cordataFRN.coroutine1);  
        if (cordataFRN.coroutine2!= null) EditorCoroutineUtility.StopCoroutine( cordataFRN.coroutine2);  
        if (cordataFRN.coroutine3!= null) EditorCoroutineUtility.StopCoroutine( cordataFRN.coroutine3);  

        if (cordataVEG.coroutine1!= null) EditorCoroutineUtility.StopCoroutine( cordataVEG.coroutine1);  
        if (cordataVEG.coroutine2!= null) EditorCoroutineUtility.StopCoroutine( cordataVEG.coroutine2);  
        if (cordataVEG.coroutine3!= null) EditorCoroutineUtility.StopCoroutine( cordataVEG.coroutine3);  

        if (cordataImage.coroutine1!= null) EditorCoroutineUtility.StopCoroutine( cordataImage.coroutine1);  

        AssetDatabase.StopAssetEditing();
        AssetDatabase.Refresh();

    }

   
}


#endif