using PSJVS.Containers;
using SharpJVS;
using System;
using System.Threading;

class Program
{
    static readonly JVSIOBoard WinJVS = new JVSIOBoard(
        JVS.JVS_HOST_ADDR, "WinJVS Test;Naoki's Retro Corner;2024",
        JVS.DEC2BCD(10), JVS.DEC2BCD(20), JVS.DEC2BCD(11),
        null
        );

    static readonly JVS jvsClass = new JVS("COM6", 115200, WinJVS);

    static int Main(string[] args)
    {
        byte machineSwitch = 0x00;
        byte[] switchData = new byte[2];
        byte[] outputData = new byte[3];

        Console.CancelKeyPress += (sender, e) =>
        {
            // This works but doesn't. It causes a exception error when closing.
            jvsClass.Close();
            Environment.Exit(0);
        };
        AppDomain.CurrentDomain.ProcessExit += delegate (object sender, EventArgs eventArgs)
        {
            jvsClass.Close();
        };

        // Get the JVS class type

        int _ioCount = jvsClass.Connect();
        if (_ioCount == 0)
        {
            Console.WriteLine("Error: No IO boards found.");
            return 1;
        }
        Console.WriteLine("Connected IO boards:");
        for (int i = 0; i < _ioCount; i++)
        {
            jvsClass.boards.Add(jvsClass.GetIOMetaDeta(i + 1));
            Console.WriteLine("\t" +
                jvsClass.boards[i].NodeID.ToString() + ": " +
                jvsClass.boards[i].Identity
            );
        }

        int _cy = Console.CursorTop;
        while (jvsClass._isRunning)
        {
            try
            {
                Console.SetCursorPosition(0, _cy);
                jvsClass.ReadSwitches(1, 1, 2);
                JVS_Frame _r = jvsClass.Read();
                if (_r != null)
                {
                    Console.WriteLine("1: P1: {0} {1}", Convert.ToString(_r.data[1], 2).PadLeft(8, '0'), Convert.ToString(_r.data[2], 2).PadLeft(8, '0'));
                }
                jvsClass.WriteOutputs(1, new byte[3] { 0x01, 0x02, 0x01 });
                jvsClass.Read();
                Thread.Sleep(5);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }


        //return jvsClass.Main();
        return 0;
    }

}
