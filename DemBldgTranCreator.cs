using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Net;


using PlateauCityGml;

/*
kaz54




PlateauCityGml
https://github.com/ksasao/PlateauCityGmlSharp
をベースにUnityで直接CityGMLファイルを読み込んでオブジェクト生成

変更箇所

PlateauCityGmlSharp-main\src\PlateauCityGml\Properties は削除
PlateauCityGmlSharp-main\src\PlateauCityGmlToObjは削除
Program.csは削除

//using System.Numerics;
using UnityEngine;


//double dist = Vector3.DistanceSquared(o.Value, pointList[i].Value);
double dist = (o.Value-pointList[i].Value).sqrMagnitude;


Posotion.csの 95-

            // return new Vector3 {
            //     X = -(float)(xp.DistanceTo(origin) * ((Longitude - origin.Longitude) >= 0 ? 1.0 : -1.0)),
            //     Y = (float)(Altitude - origin.Altitude),
            //     Z = (float)(yp.DistanceTo(origin) * ((Latitude - origin.Latitude) >= 0 ? 1.0 : -1.0))
            // };

            return new Vector3 {
                x = (float)(xp.DistanceTo(origin) * ((Longitude - origin.Longitude) >= 0 ? 1.0 : -1.0)),
                y = (float)(Altitude - origin.Altitude),
                z = (float)(yp.DistanceTo(origin) * ((Latitude - origin.Latitude) >= 0 ? 1.0 : -1.0))
            };

ModelGenerator.cs
            //    model.Add($"v {v.X} {v.Y} {v.Z}");
                model.Add($"v {v.x} {v.y} {v.z}");

                // model.Add($"vt {UV[i].X} {UV[i].Y}");
                model.Add($"vt {UV[i].x} {UV[i].y}");


//        private Building _building;
        public Building _building;
        public ModelGenerator()
        {
        }
//        private void ModelInitialize(Building building, Position origin)
        public void ModelInitialize(Building building, Position origin)

CityGMLParser　415
                    // X = Convert.ToSingle(items[i * 2]),
                    // Y = Convert.ToSingle(items[i * 2 + 1]),
                    x = Convert.ToSingle(items[i * 2]),
                    y = Convert.ToSingle(items[i * 2 + 1]),


//                Vector3 n = Vector3.Cross(P2.Value - P1.Value, P0.Value - P1.Value);
                Vector3 n = Vector3.Cross(P0.Value - P1.Value, P1.Value - P1.Value);

三角形ポリゴンへの分割は以下を使用
Triangulator - Unify Community Wiki
http://wiki.unity3d.com/index.php?title=Triangulator
https://web.archive.org/web/20210622183655/http://wiki.unity3d.com/index.php?title=Triangulator




*/

/* 地域メッシュコードを扱うクラス
緯度経度と3次メッシュコード(4桁2桁2桁)の相互変換用
add3メソッドで3次メッシュコードでオフセットした位置のメッシュコードを取得
add2メソッドで2次メッシュコードでオフセットした位置のメッシュコードを取得

https://nlftp.mlit.go.jp/ksj/old/old_data_mesh.html

*/
class GridSquareMeshCode 
{
    public string index;
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
        this.lat = lat;
        this.lon = lon;

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
    }

    // 度の値で追加
    public GridSquareMeshCode add(double addlat, double addlon)
    {
        return new GridSquareMeshCode(lat+addlat,lon+addlon);
    }

    // ３次メッシュの値で追加
    public GridSquareMeshCode add3(int z, int x)
    {
        int pqr = p *100 + q*10 + r + z;
//        Debug.Log(pqr);
        int uvw = u*100 + v*10+ w + x;
//        Debug.Log(uvw);
        //z
        int p2 = pqr / 100;
        int qr = pqr - p2*100;
        int q2 = qr / 10;
        int r2 = qr % 10;
        //x
        int u2 = uvw / 100;
        int vw = uvw - u2*100;
        int v2 = vw / 10;
        int w2 = vw % 10;
        string index2 = ""+p2+u2+q2+v2+r2+w2;
//        Debug.Log(index2);
        return new  GridSquareMeshCode(index2);
    }

    // ２次メッシュの値で追加
    public GridSquareMeshCode add2(int z, int x)
    {
        int pqr = p *100 + (q+z)*10 + r ;
//        Debug.Log(pqr);
        int uvw = u*100 + (v+x)*10+ w ;
//        Debug.Log(uvw);
        //z
        int p2 = pqr / 100;
        int qr = pqr - p2*100;
        int q2 = qr / 10;
        int r2 = qr % 10;
        //x
        int u2 = uvw / 100;
        int vw = uvw - u2*100;
        int v2 = vw / 10;
        int w2 = vw % 10;
        string index2 = ""+p2+u2+q2+v2+r2+w2;
//        Debug.Log(index2);
        return new  GridSquareMeshCode(index2);
    }

    // ２次メッシュでのブロックの差を返す
    public (int z, int x) diff2(GridSquareMeshCode l) {
        return ((q - l.q),(v- l.v));
    }
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
　PlateauCityGMLのModelGeneratorを継承してUnity上でMesh等を生成

*/
class UnityModelGenerator : ModelGenerator {
    public bool useCollider;
    public bool useTexture;
    public Material bldgMaterial;
    public UnityModelGenerator(Building building, Position origin, bool useCollider, bool useTexture, Material bldgMaterial)
    {
        ModelInitialize(building, origin);
        this.useCollider = useCollider;
        this.useTexture = useTexture;
        this.bldgMaterial = bldgMaterial;
    }
    public void Create(GameObject bldg, bool bldgAVGPosition){
        //Debug.Log(_building.Id+" "+_building.Name+" Vertices.Length:"+Vertices.Length);
        if (Vertices.Length == 0 ) 
        {
            return;
        }
        if (_building.Id == "") 
        {
            return;
        }        
        GameObject bldgX = new GameObject(_building.Id);
        var vertices = new Vector3[Vertices.Length];
        for(int i = 0; i < vertices.Length; i++) {
            vertices[i] = Vertices[i].Value;
            //Debug.Log(vertices[i]);
            // GameObject go = Instantiate(go1, vertices[i], Quaternion.identity);
            // go.name = "Pos" + i;

        }

        // x z の平均とyの最小値を求める　transformを実際の場所にするため
        Vector3 vbase = new Vector3();
        if(bldgAVGPosition) {
            vbase.y = float.MaxValue;
            for(int i = 0; i < vertices.Length; i++) {
                vbase.x += vertices[i].x;
                vbase.z += vertices[i].z;
                vbase.y = UnityEngine.Mathf.Min(vbase.y, vertices[i].y);
            }
            vbase.x /= vertices.Length;
            GameObject gobase   = GameObject.CreatePrimitive (PrimitiveType.Sphere);
            gobase.transform.parent = bldgX.transform;
            //gobase.transform.position = vbase;
                vbase.z /= vertices.Length;
            for(int i = 0; i < vertices.Length; i++) {
                vertices[i].x -= vbase.x;            
                vertices[i].z -= vbase.z;            
                vertices[i].y -= vbase.y;            
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
        if(bldgAVGPosition) {
            bldgX.transform.position = vbase;
        }

        Mesh mesh = new Mesh();
        mesh.RecalculateNormals();
        var filter = bldgX.GetComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = UV;
        mesh.RecalculateNormals();
        if (useCollider) 
        {
            bldgX.AddComponent<MeshCollider>();    
            var collider = bldgX.GetComponent<MeshCollider>();
            collider.sharedMesh  = mesh;
        }


    // Debug.Log(TextureFile);
        if (TextureFile != null && useTexture)
        {
            FileStream fs = new FileStream(TextureFile, FileMode.Open);
            BinaryReader bin = new BinaryReader(fs);
            byte[] result = bin.ReadBytes((int)bin.BaseStream.Length);
            bin.Close();

            // テクスチャを読み込み
            Texture2D tex = new Texture2D(1, 1);
            tex.LoadImage(result);
            bldgX.GetComponent<Renderer>().material.mainTexture = tex;
            bldgX.GetComponent<Renderer>().material.shader = Shader.Find ("Unlit/Texture");
//                bldgX.GetComponent<Renderer>().sharedMaterial.mainTexture = tex;

        } else {
            MeshRenderer meshRenderer = bldgX.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = bldgMaterial;
            // dem.GetComponent<Renderer>().sharedMaterial.mainTexture = tex;

        }
    }
}























public class DemBldgTranCreator : MonoBehaviour
{


    // Inspectorに表示される部分
    /* udpフォルダの場所　 */
    public string udxpath = @"C:\PLATEAU\40205_iizuka-shi_2020_citygml_5_op\40205_iizuka-shi_2020_citygml_x_op\udx\";//dem\50303564_dem_6697_op.gml";
    public string mapindex = "50303564";     // メッシュの番号　８桁の数字の文字列　
    // C:\PLATEAU\34202_kure-shi_2020_citygml_3_op\udx\
    // 51322484    
    public int xsize = 1;               // x（経度方向）に何ブロック生成するか
    public int zsize = 1;               // z（経度方向）に何ブロック生成するか
    
    public bool demON = true;           // 地形を生成するならtrue
    public bool demUseColider = true;   // 地形にColliderをアタッチするならtrue
    public bool demUseTexture = true;   // 地形に地理院地図の画像を貼るならtrue
    public Material demMaterial;        // 画像を貼らない場合のMaterial
    
    public bool roadON = false;          // 道を生成するならtrue
    public float roadSplitLength = 5;    // 長い1辺のときの分割する長さ(m)
    public float roadMargeLength = 0.5f; // 近い点をまとめる長さ(m) // 別の面との同一点はみてないのでギャップがおこるかも。
    public bool roadSlowButGood = false; // よりよい分割（とりあえず長さが最小
    public Material roadMaterial;       // 道のMaterial
    
    public bool bldgON = true;          // 建物を生成するならtrue
    public bool bldgAVGPosition = false; // 建物の位置をMeshの平均（yはMin）に
    public bool bldgUseCollider = false; // 建物にColliderをアタッチするならtrue
    public bool bldgUseTexture = false;  // 建物に画像を貼るならtrue
    public Material bldgMaterial;       // 画像を貼らない場合のMaterial



    // 基準位置
    Position baseLowerCorner;

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
                    if (!roadSlowButGood) {
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

    // 地形のメッシュを生成
    void createMesh(string mapindex, GameObject go, Position lowerCorner, Position upperCorner,XmlReader reader)
    {
        XmlDocument doc = new XmlDocument();
        var r2 = reader.ReadSubtree();
        XmlNode cd = doc.ReadNode(r2);
        XmlNodeList member = cd.ChildNodes;
        List<Surface> surfaces = new List<Surface>();
        int count = 0;
        foreach (XmlNode node in member)
        {
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

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        

        mesh.Clear();
        count = surfaces.Count;
        // Debug.Log(count);
        var vertices = new Vector3[ count* 3];
        var uv = new Vector2[count * 3];
        var triangles = new int[count * 3];

        Vector3 uvmax = upperCorner.ToVector3(lowerCorner);
        // Debug.Log(uvmax);
        // Debug.Log(mapindex);
        // Debug.Log(baseLowerCorner);
//       for (int i = 0; i < surfaces.Count; i++)
        for (int i = 0; i < count; i++)
        {
            vertices[i * 3 + 0] = surfaces[i].Positions[0].ToVector3(baseLowerCorner);
            vertices[i * 3 + 1] = surfaces[i].Positions[2].ToVector3(baseLowerCorner);
            vertices[i * 3 + 2] = surfaces[i].Positions[1].ToVector3(baseLowerCorner);
            triangles[i * 3 + 0] = i*3;
            triangles[i * 3 + 1] = i*3+1;
            triangles[i * 3 + 2] = i*3+2;

            for( int j = 0; j < 3; j++) {
                uv[i * 3 + j] = new Vector2(1.0f*vertices[i * 3 + j].x / uvmax.x, 1.0f*vertices[i * 3 + j].z / uvmax.z);
            }
        }
        Texture2D tex = getDemTexture(mapindex);
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

        if (demUseColider) {
            var collider = dem.GetComponent<MeshCollider>();
            collider.sharedMesh  = mesh;
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
//dem.GetComponent<Renderer>().material.shader = Shader.Find("Unlit/Texture");

//        dem.GetComponent<Renderer>().material.mainTexture = tex;
        //dem.GetComponent<Renderer>().sharedMaterial.mainTexture = tex;

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

    // gmlファイルから地形を生成
    private void makeDEM(string mapindex, GameObject go, Position lowerCorner, Position upperCorner, string gmlPath)
    {
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
                       createMesh(mapindex, go,  lowerCorner,  upperCorner,reader);
                    }

                }

            }
        }
    }

    // 道路を生成
    void createRoadFromReader(string currentMapIndex, GameObject go, Position lowerCorner, Position upperCorner, float maxheight,XmlReader reader)
    {
        Vector3 uvmax = upperCorner.ToVector3(lowerCorner);
        Debug.Log("Road currentMapIndex "+currentMapIndex+" lowerCorner "+ lowerCorner+" upperCorner "+upperCorner+" uvmax "+uvmax);

        GameObject road = new GameObject("Tran");
        road.transform.parent = go.transform;


        XmlDocument doc = new XmlDocument();
        var r2 = reader.ReadSubtree();
        XmlNode cd = doc.ReadNode(r2);
        XmlNodeList member = cd.ChildNodes;
        List<Surface> surfaces = new List<Surface>();
        int index1 = 0;
        foreach (XmlNode node in member)
        {
            //Debug.Log(node.Name);
            if (node.Name == "core:cityObjectMember")
            {
                if (true) //(index1 == 14283) //(true) //(index1 <= 26) // (true) //index1 == 26)// 2) // 26)
                {
                    // Debug.Log("Road " + index1);
                    // Debug.Log(node.InnerText);
                    // Debug.Log(node.FirstChild.FirstChild.FirstChild.Name);

                    Surface s = new Surface();

                    XmlNodeList surfaceMembers = node.FirstChild.FirstChild.FirstChild.ChildNodes;
                    int index2 = 0;
                    foreach (XmlNode subnode in surfaceMembers)
                    {
                        if (true) //index2 == 2)
                        {

                            string posStr = subnode.InnerText;
                            GameObject roadX = new GameObject("Road" + index1.ToString("000000") + "-" + index2.ToString("00"));
                            roadX.transform.parent = road.transform;
                            roadX.AddComponent<MeshFilter>();
                            roadX.AddComponent<MeshRenderer>();
                            roadX.AddComponent<MeshCollider>();
                            s.SetPositions(Position.ParseString(posStr));
                            
                            int len = s.Positions.Length;
                            // Debug.Log("len " + len);
                            Vector3[] vertices = new Vector3[len];
                            List<Vector3> verticesList = new List<Vector3>();

                            // 範囲上になければ次へ進む(continue) 地面が３次メッシュで道路が２次メッシュの場合への対応
                            bool inmap = false;
                            for (int i = 0; i < len; i++)
                            {
                                Vector3 v = s.Positions[i].ToVector3(baseLowerCorner);
                                if (lowerCorner.Latitude < s.Positions[i].Latitude && 
                                s.Positions[i].Latitude < upperCorner.Latitude  && 
                                lowerCorner.Longitude < s.Positions[i].Longitude && 
                                s.Positions[i].Longitude < upperCorner.Longitude
                                ) {
                                    inmap = true;
                                }
                                verticesList.Add(v);
                            }
                            if (!inmap) {
                                continue;
                            }
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
                                    if (Vector3.Distance(v, vprev) > roadMargeLength)  { 
                                        if (roadSplitLength> 0) {
                                            // 長い線は分割(hiro)
                                            Vector3 diff = v - vprev;  //距離の差分
                                            int count = (int)(distance / roadSplitLength); // roadSplitLength m おき
                                            for(int j = 1; j < count; j++)
                                            {
                                                verticesList2.Add(vprev + diff*j / count);
                                            }
                                        }
                                        verticesList2.Add(v);
                                    }                                    
                                }

                            }
                            // Debug.Log("verticesList2 "+verticesList2.Count);


                            // 地面に向けてRaycastを飛ばし高さを決める
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

                                Ray ray = new Ray(new Vector3(v.x, maxheight + 30, v.z), -Vector3.up);
                                RaycastHit hit = new RaycastHit();
                                if (Physics.Raycast(ray, out hit, maxheight + 30))
                                {
                                    v.y = hit.point.y + 0.1f;
                                    //Debug.Log(" hit.point "+hit.point);
                                } else
                                {
                                    //Debug.Log("not hit");
                                    
                                }
                                verticesList3.Add(v);
                                Debug.DrawRay(ray.origin, ray.direction * uvmax.y * 3, Color.red, 5);
                            }
                            //Debug.Log(" index1: "+index1+" verticesList3: "+verticesList3.Count);

                            if (verticesList3.Count <=3){
                                continue;
                            }

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

                            MeshRenderer meshRenderer = roadX.GetComponent<MeshRenderer>();
                            meshRenderer.sharedMaterial = roadMaterial;
                            
                        }
                        index2++;
                    }


                }
                index1++;
            }
        }
    }


    // gmlファイルから道路を生成
    private void makeTRAN(string currentMapIndex, GameObject goCurrent, Position lowerCorner, Position upperCorner, float maxheight, string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;

        using (var fileStream = File.OpenText(gmlPath))
        using (XmlReader reader = XmlReader.Create(fileStream, settings))
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "core:CityModel")
                    {
                        createRoadFromReader( currentMapIndex,  goCurrent,  lowerCorner,  upperCorner, maxheight, reader);
                    }
                }

            }
        }
    }

    // gmlファイルから建物を生成
    private void makeBLDG(string currentMapIndex, GameObject goCurrent, Position lowerCorner, Position upperCorner,string gmlPath)
    {
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.IgnoreWhitespace = true;
        GameObject bldg = new GameObject("Bldg");
        bldg.transform.parent = goCurrent.transform;

        CityGMLParser cgp = new CityGMLParser(lowerCorner, upperCorner);
        var buildings = cgp.GetBuildings( gmlPath);
        for (int i = 0; i < buildings.Length; i++)
        {
            Building b = buildings[i];
            UnityModelGenerator mg = new UnityModelGenerator(b, baseLowerCorner, bldgUseCollider, bldgUseTexture, bldgMaterial);
            mg.Create(bldg,bldgAVGPosition);
        }
    }

    // udxpath中のmapindexと種類(dem/tran/bldg)からファイルのパスを取得（８桁の３次メッシュの場合）
    public string getPath(string udxpath, string mapindex, string dirname) 
    {
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


    // DEM用のテクスチャの画像をダウンロード　タイルごとなのでメッシュとはズレがある
    public void getImage(string mapindex, int x, int y){

        string path = Application.dataPath;
        string filename = path+@"\..\cyberjapandata-"+mapindex+"-"+y+"-"+x+".jpg";
        if (System.IO.File.Exists(filename))
        {
            // Debug.Log("File exists!");
            return;
        }
        string url=@"https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/18/"+x+"/"+y+".jpg";
        try
        {

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            WebResponse res = req.GetResponse();
            Stream st = res.GetResponseStream();

            byte[] buffer = new byte[65535];
            MemoryStream ms = new MemoryStream();
            while (true)
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
            // Debug.Log(path);
            FileStream fs = new FileStream(filename, FileMode.Create);
            byte[] wbuf = new byte[ms.Length];
            ms.Seek(0, SeekOrigin.Begin);
            ms.Read(wbuf, 0, wbuf.Length);
            fs.Write(wbuf, 0, wbuf.Length);
            fs.Close();
        }
        catch (WebException e)
        {
            Debug.Log("Error "+url+" "+e);
        }

    }

    // DEM用の画像をつなぎ合わせてトリミングしてTextureを返す
    Texture2D getDemTexture(string mapindex) {
        string path = Application.dataPath;
        string filename = path+@"\..\dem-"+mapindex + ".jpg";

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

        if (!System.IO.File.Exists(filename))
        {
            Debug.Log("Create DEM Texture");

            // 範囲の画像をダウンロード
            for(int y = mtu.y; y <= mtl.y; y++) 
            {
                for(int x = mtl.x; x <= mtu.x; x++) 
                {
                    getImage(mapindex,x,y);
                }
            }
        
            // テクスチャに画像を貼り付け
            Texture2D texture = new Texture2D(w*256, h*256);
            for(int y = 0; y < h; y++) 
            {
                for(int x = 0; x < w; x++) 
                {
                    byte[] bytes = File.ReadAllBytes(path+@"\..\cyberjapandata-"+mapindex+"-"+(mtu.y+y)+"-"+(mtl.x+x)+".jpg");
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
        Texture2D textureL2 = new Texture2D(w*256, h*256);
        byte[] bytesL2 = File.ReadAllBytes(filename);
        textureL2.LoadImage(bytesL2);            
        return textureL2;
    }


    [ContextMenu("Create")]
    private void makeAll()
    {
        //getImage(mapindex,226233,105038);
        int roadtype = 8;
        string baseMapIndex = mapindex;
        string last = udxpath.Substring(udxpath.Length-1);
        // Debug.Log(last);
        if (last !=@"\") {
            udxpath +=@"\";
        }
        string basedemfilename = getPath(udxpath, baseMapIndex, "dem");
        baseLowerCorner = getBaseLowerCorner(basedemfilename);
        string basetranfilename = getPath(udxpath, baseMapIndex, "tran");
        if (basetranfilename == null) {
            basetranfilename = getPath6(udxpath, baseMapIndex, "tran");
            if (basetranfilename != null) {
                roadtype = 6;
            }
        }
        // Debug.Log("roadtype:"+roadtype);
        GridSquareMeshCode mibase = new GridSquareMeshCode(mapindex);
        
        string currentMapIndex = baseMapIndex;
        float maxheight = 0;
        for(int z = 0; z < zsize; z++) 
        {
            for(int x = 0; x < xsize; x++) 
            {
                GridSquareMeshCode mi = mibase.add3(z,x);
                currentMapIndex = mi.index;
                string demfilename = getPath(udxpath, currentMapIndex, "dem");
                string tranfilename = getPath(udxpath, currentMapIndex, "tran");
                string bldgfilename = getPath(udxpath, currentMapIndex, "bldg");
                if (demfilename == null ){
                     continue;
                }
                Debug.Log("dem "+demfilename);
                Debug.Log("tran "+tranfilename);
                Debug.Log("bldg "+bldgfilename);            
                Debug.Log("MeshCode "+currentMapIndex);
                var corners = getCorner(demfilename);

                GameObject goCurrent = new GameObject(currentMapIndex);
                goCurrent.transform.parent = transform;

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



                if (demON && demfilename != null) 
                {
                    makeDEM(currentMapIndex, goCurrent, corners.lowerCorner, corners.upperCorner, demfilename);
                }
                if (roadON && tranfilename != null)
                {
                   makeTRAN(currentMapIndex, goCurrent, corners.lowerCorner, corners.upperCorner,maxheight, tranfilename);

                }                
                if (bldgON && bldgfilename != null) 
                {
                    makeBLDG(currentMapIndex, goCurrent, corners.lowerCorner, corners.upperCorner,bldgfilename);
                }
            }
        }
        Debug.Log("roadtype "+roadtype);
        // Debug.Log(" maxheight "+  maxheight );
        if (roadtype == 6) {
            // MapIndex mi6upper = mibase.add3(zsize,xsize); //右上位置の2次メッシュでのコード
            // Debug.Log("mi6upper "+mi6upper.index);
            // //MapIndex mi6upperplus = mibase.add3(zsize+1,xsize+1); 
            // string baseUpperdemfilename = getPath(udxpath, mi6upper.index, "dem");
            // Position baseUpperCorner = getBaseLowerCorner(baseUpperdemfilename); 

            GridSquareMeshCode mi6upperBlock = mibase.add3(zsize-1,xsize-1); // ３次メッシュでの右上位置ブロックのコードを取得
            //Debug.Log("mi6upperBlock "+mi6upperBlock.index);
            string baseUpperdemfilename = getPath(udxpath, mi6upperBlock.index, "dem");
            (Position tmpLowwerCorner, Position baseUpperCorner) = getCorner(baseUpperdemfilename); // 右上の位置を取得

            (int zsize6, int xsize6) = mi6upperBlock.diff2(mibase);// ２次メッシュでのブロック数を取得
            // Debug.Log("diff "+xsize6+" "+zsize6);
            // MapIndex mitest = mibase.add2(0,0);
            // Debug.Log(mitest.index); 

            // ２次メッシュでの繰り返し、
            for(int z = 0; z <= zsize6; z++) 
            {
                // Debug.Log("z"+z);
                for(int x = 0; x <= xsize6; x++) 
                {
                    GridSquareMeshCode mi = mibase.add2(z,x); 
                    // Debug.Log(mibase.index+" "+mi.index);         
                    currentMapIndex = mi.index.Substring(0,6);
                    // Debug.Log(currentMapIndex);                      
                    string tranfilename = getPath6(udxpath, currentMapIndex, "tran");   
                    Debug.Log("tran "+tranfilename);
                    GameObject goCurrent = new GameObject(currentMapIndex);
                    goCurrent.transform.parent = transform; 
                    // Debug.Log(tranfilename);
                    if (roadON && tranfilename != null)
                    {
                       makeTRAN(currentMapIndex, goCurrent, baseLowerCorner, baseUpperCorner,maxheight, tranfilename);
                    }                     
                }
            }
        }
    }
}