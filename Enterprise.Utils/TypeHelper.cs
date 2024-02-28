namespace Enterprise.Utils;

public static class TypeHelper
{
    public static string ExtractName(this Type type)
    {
        string name = type.Name.Split("`")[0];
        if (type.IsGenericType)
        {
            Type[] arguments = type.GetGenericArguments();
            name += " <";
            var i = 0;
            foreach (Type t in arguments)
            {
                if (i++ != 0) name += ", ";
                name += ExtractName(t);
            }
            name += ">";
        }
        return name.ToString();
    }
}
