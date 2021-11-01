// Name: Lectvs Composite Image
// Author: lectvs
// Version: 1.0
// Desc: Composite (layers + metadata) image for use with lectvs game engine
// LoadExtns: .lci
// SaveExtns: .lci
// Flattened: true

#region UICode
#endregion

private const string HeaderSignature = ".LCI";
private const string Base64DataStart = "data:image/png;base64,";
private const string LayerDataDelimiter = "|";
private const string LayerDataAssignmentDelimiter = "=";
private const string DefaultsLayerName = "defaults";

private Dictionary<int, int> PDNBlendModesToPixiBlendModes = new Dictionary<int, int> {
    {0, 0},   // Normal
    {1, 2},   // Multiply
    {2, 1},   // Additive
    {3, 8},   // ColorBurn
    {4, 7},   // ColorDodge
    {5, -1},  // Reflect
    {6, -1},  // Glow
    {7, 4},   // Overlay
    {8, 11},  // Difference
    {9, -1},  // Negation
    {10, 6},  // Lighten
    {11, 5},  // Darken
    {12, 3},  // Screen
    {13, -1}, // Xor
};

private string[] split(string str, string by) {
    return str.Split(new string[] { by }, StringSplitOptions.None);
}

class Pt {
    public float x { get; set; }
    public float y { get; set; }

    public Pt() : this(0, 0) {}

    public Pt(float x, float y) {
        this.x = x;
        this.y = y;
    }

    public Pt(Pt from) : this() {
        if (from != null) {
            this.x = from.x;
            this.y = from.y;
        }
    }
}

class Rect {
    public float x { get; set; }
    public float y { get; set; }
    public float width { get; set; }
    public float height { get; set; }

    public Rect() : this(0, 0, 0, 0) {}

    public Rect(float x, float y, float width, float height) {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    public Rect(Rect from) : this() {
        if (from != null) {
            this.x = from.x;
            this.y = from.y;
            this.width = from.width;
            this.height = from.height;
        }
    }
}

class DocumentData {
    public int width { get; set; }
    public int height { get; set; }
    public LayerData[] layers { get; set; }
}

class LayerData {
    public string rawName { get; set; }
    public string name { get; set; }
    public string image { get; set; }
    public Pt position { get; set; }
    public bool isDataLayer { get; set; }
    public LayerProperties properties { get; set; }

    public bool visible { get; set; }
    public byte opacity { get; set; }
    public int blendMode { get; set; }

    // Paint.NET specific.
    public int offsetX { get; set; }
    public int offsetY { get; set; }
}

class LayerProperties {
    public bool restrict { get; set; }
    public string layer { get; set; }
    public Pt anchor { get; set; }
    public Pt offset { get; set; }
    public string physicsGroup { get; set; }
    public Rect bounds { get; set; }
    public string placeholder { get; set; }
    public Rect[] multiBounds { get; set; }
    public Dictionary<string, string> data { get; set; }

    public LayerProperties() {
        restrict = false;
        layer = null;
        anchor = null;
        offset = null;
        physicsGroup = null;
        bounds = null;
        placeholder = null;
        data = new Dictionary<string, string>();
    }

    public LayerProperties(LayerProperties from) : this() {
        if (from != null) {
            restrict = from.restrict;
            layer = from.layer;
            anchor = from.anchor == null ? null : new Pt(from.anchor);
            offset = from.offset == null ? null : new Pt(from.offset);
            physicsGroup = from.physicsGroup;
            bounds = from.bounds == null ? null : new Rect(from.bounds);
            placeholder = from.placeholder;
            data = new Dictionary<string, string>(from.data);
        }
    }
}

void ValidateImage(Document input) {
    HashSet<string> usedNames = new HashSet<string>();

    for (int l = 0; l < input.Layers.Count; l++) {
        BitmapLayer layer = (BitmapLayer)input.Layers[l];

        string name = extractLayerName(layer.Name);

        if (usedNames.Contains(name)) {
            throw new Exception("Name \"" + name + "\" is used for multiple layers. Layers must have unique names.");
        }

        usedNames.Add(name);
    }
}

void SaveImage(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback) {
    ValidateImage(input);

    DocumentData documentData = new DocumentData();
    documentData.width = input.Width;
    documentData.height = input.Height;

    LayerProperties defaultLayerProperties = getDefaultLayerProperties(input);

    LayerData[] layers = new LayerData[input.Layers.Count];
    for (int l = 0; l < input.Layers.Count; l++) {
        BitmapLayer layer = (BitmapLayer)input.Layers[l];

        LayerProperties layerProperties = extractLayerProperties(layer, defaultLayerProperties);

        LayerData layerData = new LayerData();
        layerData.rawName = layer.Name;
        layerData.name = extractLayerName(layer.Name);
        layerData.visible = layer.Visible;
        layerData.opacity = layer.Opacity;
        layerData.blendMode = pdnBlendModeToPixiBlendMode((int)layer.BlendMode);

        layerData.properties = layerProperties;

        // Layer is a data layer if any apply:
        layerData.isDataLayer = layerData.name == DefaultsLayerName    // Layer is the "defaults" layer
                             || layerData.name.StartsWith("//")        // Layer is a comment layer
                             || layerProperties.placeholder != null    // Object is placeholder
                             || layerProperties.multiBounds != null;    // Object is a multibounds

        Rectangle contentBounds = layerProperties.restrict
                ? getRestrictedBounds(layer.Surface)
                : new Rectangle(0, 0, input.Width, input.Height);
        layerData.offsetX = contentBounds.X;
        layerData.offsetY = contentBounds.Y;

        layerData.position = new Pt(contentBounds.X, contentBounds.Y);
        if (layerProperties.anchor != null) {
            // Round anchor to nearest pixel
            layerProperties.anchor.x = (float)Math.Floor(contentBounds.Width * layerProperties.anchor.x) / contentBounds.Width;
            layerProperties.anchor.y = (float)Math.Floor(contentBounds.Height * layerProperties.anchor.y) / contentBounds.Height;

            layerData.position.x += contentBounds.Width * layerProperties.anchor.x;
            layerData.position.y += contentBounds.Height * layerProperties.anchor.y;
        }
        if (layerProperties.offset != null) {
            layerData.position.x -= layerProperties.offset.x;
            layerData.position.y -= layerProperties.offset.y;
        }
        
        // Render image data as Base64-encoded PNG.
        using (MemoryStream ms = new MemoryStream()) {
            Bitmap bitmap = layer.Surface.CreateAliasedBitmap(contentBounds);
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            string base64 = System.Convert.ToBase64String(ms.ToArray());
            layerData.image = Base64DataStart + base64;
        }

        layers[l] = layerData;
    }

    documentData.layers = layers;

    string documentJson = System.Text.Json.JsonSerializer.Serialize<DocumentData>(documentData);
    string documentString = HeaderSignature + documentJson;

    // The stream paint.net hands us must not be closed.
    using (StreamWriter writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true)) {
        writer.Write(documentString);
    }
}

Document LoadImage(Stream input) {
    Document doc = null;

    // The stream paint.net hands us must not be closed.
    using (StreamReader reader = new StreamReader(input, Encoding.UTF8, leaveOpen: true)) {
        string documentString = reader.ReadLine();

        // Read and validate the file header.
        if (!documentString.StartsWith(HeaderSignature)) {
            throw new FormatException("Invalid file signature.");
        }

        string documentJson = documentString.Substring(HeaderSignature.Length);
        DocumentData documentData = System.Text.Json.JsonSerializer.Deserialize<DocumentData>(documentJson);

        // Create a new Document.
        doc = new Document(documentData.width, documentData.height);

        for (int l = 0; l < documentData.layers.Length; l++) {
            LayerData layerData = documentData.layers[l];

            byte[] imageBytes = System.Convert.FromBase64String(layerData.image.Substring(Base64DataStart.Length));

            BitmapLayer layer = null;
            using (MemoryStream ms = new MemoryStream(imageBytes)) {
                layer = new BitmapLayer(doc.Width, doc.Height);
                layer.Surface.CopySurface(Surface.CopyFromBitmap(new Bitmap(ms)), new Point(layerData.offsetX, layerData.offsetY));
            }

            layer.Name = layerData.rawName;
            layer.Visible = layerData.visible;
            layer.Opacity = layerData.opacity;
            layer.BlendMode = (LayerBlendMode)pixiBlendModeToPdnBlendMode(layerData.blendMode);

            doc.Layers.Add(layer);
        }
    }

    return doc;
}

string extractLayerName(string data) {
    string[] datas = split(data, LayerDataDelimiter);
    return datas[0];
}

LayerProperties extractLayerProperties(BitmapLayer layer) {
    return extractLayerProperties(layer, null);
}

LayerProperties extractLayerProperties(BitmapLayer layer, LayerProperties defaults) {
    LayerProperties layerProperties = new LayerProperties(defaults);

    string data = layer.Name;
    string[] datas = split(data, LayerDataDelimiter);

    for (int i = 1; i < datas.Length; i++) {
        string[] kv = split(datas[i], LayerDataAssignmentDelimiter);
        if (String.IsNullOrWhiteSpace(kv[0])) {
            throw new Exception("Layer \"" + data + "\" has a blank property section.");
        }

        if (kv.Length == 1) kv = new string[] { kv[0], "true" };

        if (!Char.IsLetter(kv[0][0])) {
            throw new Exception("Invalid variable: " + kv[0]);
        }

        if (kv[0] == "restrict") {
            layerProperties.restrict = kv[1] == "true";
        } else if (kv[0] == "layer") {
            layerProperties.layer = kv[1];
        } else if (kv[0] == "anchor") {
            layerProperties.anchor = getAnchorPoint(kv[1]);
        } else if (kv[0] == "offset") {
            layerProperties.offset = getOffsetPoint(kv[1]);
        } else if (kv[0] == "physicsGroup") {
            layerProperties.physicsGroup = kv[1];
        } else if (kv[0] == "bounds") {
            layerProperties.bounds = getBoundsRect(kv[1]);
        } else if (kv[0] == "placeholder") {
            layerProperties.placeholder = kv[1];
        } else if (kv[0] == "multiBounds") {
            if (kv[1] == "true") {
                layerProperties.multiBounds = layerToMultiBounds(layer);
            }
        } else {
            layerProperties.data[kv[0]] = datas[i].Substring(kv[0].Length + LayerDataAssignmentDelimiter.Length);
        }
    }

    return layerProperties;
}

Pt getAnchorPoint(string anchor) {
    if (anchor == "top_left") return new Pt(0, 0);
    if (anchor == "top_center") return new Pt(0.5f, 0);
    if (anchor == "top_right") return new Pt(1, 0);
    if (anchor == "center_left") return new Pt(0, 0.5f);
    if (anchor == "center_center") return new Pt(0.5f, 0.5f);
    if (anchor == "center_right") return new Pt(1, 0.5f);
    if (anchor == "bottom_left") return new Pt(0, 1);
    if (anchor == "bottom_center") return new Pt(0.5f, 1);
    if (anchor == "bottom_right") return new Pt(1, 1);
    if (anchor == "top") return new Pt(0.5f, 0);
    if (anchor == "bottom") return new Pt(0.5f, 1);
    if (anchor == "left") return new Pt(0, 0.5f);
    if (anchor == "right") return new Pt(1, 0.5f);
    if (anchor == "center") return new Pt(0.5f, 0.5f);
    
    return parsePt(anchor, "anchor");
}

Pt getOffsetPoint(string offset) {
    return parsePt(offset, "offset");
}

Rect getBoundsRect(string bounds) {
    string[] parts = split(bounds, ",");
    if (parts.Length == 4) {
        try {
            return new Rect(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
        } catch (Exception e) {
            // Pass, exception thrown below.
            e.GetHashCode();
        }
    }
    
    throw new Exception("Invalid bounds: " + bounds);
}

LayerProperties getDefaultLayerProperties(Document input) {
    for (int l = 0; l < input.Layers.Count; l++) {
        BitmapLayer layer = (BitmapLayer)input.Layers[l];
        if (extractLayerName(layer.Name) == DefaultsLayerName) {
            return extractLayerProperties(layer);
        }
    }
    return null;
}

Rectangle getRestrictedBounds(Surface surface) {
    int left = 0;
    while (left < surface.Width) {
        bool containsOpaquePixel = false;
        for (int y = 0; y < surface.Height; y++) {
            if (surface[left, y].A != 0) {
                containsOpaquePixel = true;
                break;
            }
        }
        if (containsOpaquePixel) break;
        left++;
    }

    int right = surface.Width;
    while (right > left) {
        bool containsOpaquePixel = false;
        for (int y = 0; y < surface.Height; y++) {
            if (surface[right-1, y].A != 0) {
                containsOpaquePixel = true;
                break;
            }
        }
        if (containsOpaquePixel) break;
        right--;
    }

    int top = 0;
    while (top < surface.Height) {
        bool containsOpaquePixel = false;
        for (int x = 0; x < surface.Width; x++) {
            if (surface[x, top].A != 0) {
                containsOpaquePixel = true;
                break;
            }
        }
        if (containsOpaquePixel) break;
        top++;
    }

    int bottom = surface.Height;
    while (bottom > top) {
        bool containsOpaquePixel = false;
        for (int x = 0; x < surface.Width; x++) {
            if (surface[x, bottom-1].A != 0) {
                containsOpaquePixel = true;
                break;
            }
        }
        if (containsOpaquePixel) break;
        bottom--;
    }

    int width = right - left;
    int height = bottom - top;

    if (width == 0 || height == 0) {
        return new Rectangle(0, 0, 1, 1);
    }

    return new Rectangle(left, top, right-left, bottom-top);
}

Rect[] layerToMultiBounds(BitmapLayer layer) {
    List<Rect> rects = getInitialRectsFromBitmapLayer(layer);
    optimizeCollisionRects(rects, false);  // Not optimizing entire array first to save some cycles.
    optimizeCollisionRects(rects, true);
    return rects.ToArray();
}

List<Rect> getInitialRectsFromBitmapLayer(BitmapLayer layer) {
    List<Rect> rects = new List<Rect>();
    for (int y = 0; y < layer.Surface.Height; y++) {
        for (int x = 0; x < layer.Surface.Width; x++) {
            if (layer.Surface[x, y].A > 0) {
                // Try to create as big of a horizontal line as possible.
                Rect rect = new Rect(x, y, 0, 1);
                while (x < layer.Surface.Width && layer.Surface[x, y].A > 0) {
                    rect.width++;
                    x++;
                }
                rects.Add(rect);
            }
        }
    }
    return rects;
}

void optimizeCollisionRects(List<Rect> rects, bool all) {
    int i = 0;
    while (i < rects.Count) {
        int j = i + 1;
        while (j < rects.Count) {
            bool combined = combineRects(rects[j], rects[i]);
            if (combined) {
                string[] g;
                rects.RemoveAt(j);
            } else if (all) {
                j++;
            } else {
                break;
            }
        }
        i++;
    }
}

bool combineRects(Rect rect, Rect into) {
    if (rectContainsRect(into, rect)) return true;
    if (rectContainsRect(rect, into)) {
        into.x = rect.x;
        into.y = rect.y;
        into.width = rect.width;
        into.height = rect.height;
        return true;
    }
    if (rect.x == into.x && rect.width == into.width) {
        if (rect.y <= into.y + into.height && rect.y + rect.height >= into.y) {
            float newY = Math.Min(rect.y, into.y);
            float newH = Math.Max(rect.y + rect.height, into.y + into.height) - newY;
            into.y = newY;
            into.height = newH;
            return true;
        }
    }
    if (rect.y == into.y && rect.height == into.height) {
        if (rect.x <= into.x + into.width && rect.x + rect.width >= into.x) {
            float newX = Math.Min(rect.x, into.x);
            float newW = Math.Max(rect.x + rect.width, into.x + into.width) - newX;
            into.x = newX;
            into.width = newW;
            return true;
        }
    }
    return false;
}

bool rectContainsRect(Rect rect, Rect contains) {
    return rect.x <= contains.x && rect.x + rect.width  >= contains.x + contains.width
        && rect.y <= contains.y && rect.y + rect.height >= contains.y + contains.height;
}



int pdnBlendModeToPixiBlendMode(int pdnBlendMode) {
    int pixiBlendMode = PDNBlendModesToPixiBlendModes[pdnBlendMode];
    if (pixiBlendMode == -1) {
        throw new Exception("Blend mode is not supported: " + pdnBlendMode);
    }
    return pixiBlendMode;
}

int pixiBlendModeToPdnBlendMode(int pixiBlendMode) {
    foreach (KeyValuePair<int, int> entry in PDNBlendModesToPixiBlendModes) {
        if (entry.Value == pixiBlendMode) return entry.Key;
    }
    throw new Exception("Illegal blend mode in LCI document: " + pixiBlendMode);
}

Pt parsePt(string pt, string name) {
    string[] parts = split(pt, ",");
    if (parts.Length == 2) {
        try {
            return new Pt(float.Parse(parts[0]), float.Parse(parts[1]));
        } catch (Exception e) {
            // Pass, exception thrown below.
            e.GetHashCode();
        }
    }
    
    throw new Exception("Invalid " + name + ": " + pt);
}
