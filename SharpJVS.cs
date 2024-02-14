using System;
using System.Reflection;
using SharpJVS;

class Program
{
    static void Main(string[] args)
    {
        // Get the JVS class type
        JVS jvsClass = new JVS("COM3");
        jvsClass.Main();
    }
}
