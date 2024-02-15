using SharpJVS;

class Program
{
    static int Main(string[] args)
    {
        // Get the JVS class type
        JVS jvsClass = new JVS("COM6", 115200, false, false);
        return jvsClass.Main();
    }
}
