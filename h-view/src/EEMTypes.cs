namespace Hai.ExternalExpressionsMenu;

public class EMManifest
{
    public EMExpression[] expressionParameters;
    public EMContact[] contactParameters;
    public EMPhysBone[] physBoneParameters;
    public EMMenu[] menu;
}

public class EMMenu
{
    public string label;
    public string icon;
    public string type;
    public string parameter;
    public float value;
    public int subMenuId;
    public EMMenu[] subMenu;
    public bool isSubMenuRecursive;
    public EMAxis axis0;
    public EMAxis axis1;
    public EMAxis axis2;
    public EMAxis axis3;
}

public class EMAxis
{
    public string parameter;
    public string label;
    public string icon;
}

public class EMExpression
{
    public string parameter;
    public string type;
    public bool saved;
    public bool synced;
    public float defaultValue;
}

public class EMContact
{
    public string parameter;
    public string receiverType;
    public float radius;
    public float lossyScaleX;
    public float lossyScaleY;
    public float lossyScaleZ;
}

public class EMPhysBone
{
    public string parameter;
    public float maxStretch;
    public float maxSquish;
    public string limitType;
    public float maxAngleX;
    public float maxAngleZ;
}