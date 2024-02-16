using PSJVS.Containers;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpJVS
{
    public class JVS
    {
        public const byte BUFFER_FULL = 1;
        public const byte BUFFER_ADD_ERROR = 2;
        public const byte BUFFER_READ_ERROR = 3;
        public const byte JVS_BUFFER_SIZE = 1;

        public const bool JVS_SENSE_INACTIVE = false;
        public const bool JVS_SENSE_ACTIVE = true;

        public const byte HOST_NODE = 1;
        public const byte DEVICE_NODE = 0;

        public const UInt32 JVS_DEFAULT_BUAD = 115200;

        public const byte JVS_BROADCAST_ADDR = 0xFF;
        public const byte JVS_HOST_ADDR = 0x00;

        public const byte JVS_SYNC = 0xE0;
        public const byte JVS_MARK = 0xD0;

        // Broadcast commands
        public const byte JVS_RESET_CODE = 0xF0;
        public const byte JVS_SETADDR_CODE = 0xF1;
        public const byte JVS_COMCHG_CODE = 0xF2;
        // Init commands
        public const byte JVS_IOIDENT_CODE = 0x10;
        public const byte JVS_CMDREV_CODE = 0x11;
        public const byte JVS_JVSREV_CODE = 0x12;
        public const byte JVS_COMVER_CODE = 0x13;
        public const byte JVS_FEATCHK_CODE = 0x14;
        public const byte JVS_MAINID_CODE = 0x15;
        // Data I/O commands
        public const byte JVS_READSWITCH_CODE = 0x20;
        public const byte JVS_READCOIN_CODE = 0x21;
        public const byte JVS_READANALOG_CODE = 0x22;
        public const byte JVS_READROTARY_CODE = 0x23;
        public const byte JVS_READKEY_CODE = 0x24;
        public const byte JVS_READSCREENPOS_CODE = 0x25;
        public const byte JVS_READMISC_CODE = 0x26;
        // Output commands
        public const byte JVS_READPAYOUT_CODE = 0x2E;
        public const byte JVS_DATARETRY_CODE = 0x2F;
        public const byte JVS_COINDECREASE_CODE = 0x30;
        public const byte JVS_PAYOUTINCREASE_CODE = 0x31;
        public const byte JVS_GENERICOUT1_CODE = 0x32;
        public const byte JVS_ANALOGOUT_CODE = 0x33;
        public const byte JVS_CHARACTEROUT_CODE = 0x34;
        public const byte JVS_COININCREASE_CODE = 0x35;
        public const byte JVS_PAYOUTDECREASE_CODE = 0x36;
        public const byte JVS_GENERICOUT2_CODE = 0x37;    // Sega Type 1 IO does not support this command
        public const byte JVS_GENERICOUT3_CODE = 0x38;      // Sega Type 1 IO does not support this command

        // Commands = 0x60 to = 0x7F are manufacturer specific and not covered here

        // Status code
        public const byte JVS_STATUS_NORMAL = 1;
        public const byte JVS_STATUS_UNKNOWNCMD = 2;      // Sega IO sends this if there is a parameter error
        public const byte JVS_STATUS_CHECKSUMERROR = 3;
        public const byte JVS_STATUS_OVERFLOW = 4;       // Sega IO sends this back when it receives a empty packet

        // Report codes
        public const byte JVS_REPORT_NORMAL = 1;
        public const byte JVS_REPORT_PARAMETERERROR = 2;
        public const byte JVS_REPORT_DATAERROR = 3;
        public const byte JVS_REPORT_BUSY = 4;

        // Coin Condition codes
        public const byte JVS_COIN_NORMAL = 0;
        public const byte JVS_COIN_JAM = 1;
        public const byte JVS_COIN_NOCOUNTER = 2;
        public const byte JVS_COIN_BUSY = 3;

        // JVS Feature list (for use with jvs_message):
        public const int JVS_FEATURE_END = 0;
        public const int JVS_FEATURE_SWITCH = 1;
        public const int JVS_FEATURE_COIN = 2;
        public const int JVS_FEATURE_ANALOG = 3;
        public const int JVS_FEATURE_ROTARY = 4;
        public const int JVS_FEATURE_KEYCODE = 5;
        public const int JVS_FEATURE_SCREEN = 6;
        public const int JVS_FEATURE_MISC = 7;
        public const int JVS_FEATURE_CARD = 16;
        public const int JVS_FEATURE_MEDAL = 17;
        public const int JVS_FEATURE_GPO = 18;
        public const int JVS_FEATURE_ANALOG_OUT = 19;
        public const int JVS_FEATURE_CHARACTER = 20;
        public const int JVS_FEATURE_BACKUP = 21;

        // JVS character output types (for use with jvs_message):
        public const int JVS_CHARACTER_ASCII = 1;
        public const int JVS_CHARACTER_ALPHA = 2;
        public const int JVS_CHARACTER_KATA = 3;
        public const int JVS_CHARACTER_KANJI = 4;

        public List<JVSIOBoard> _boards = new List<JVSIOBoard> { };

        private bool _isRunning = true;

        private SerialPort _comPort;
        private readonly bool  _useDSR = false;
        private readonly bool _useCD = false;
        private readonly bool _useDTR = false;
        private readonly string _comPortName;
        private readonly Int32 _comPortBaud;

        private byte thisNodeID = JVS_HOST_ADDR;
        private readonly JVSIOBoard thisNodeInfo = null;
        private JVS_Frame TXBuffer = null;
        private JVS_Frame RXBuffer = null;
        private bool _rxFlag = false;

        // IO Data arrays
        public byte machineSwitches;
        public byte[] playerArray = null;
        public int[] coinSlots = null;     // Pointer to coin slot counter, read as: literal uint counter [per player]
        public byte[] coinCondition = null;     // Coin conditions [per player]
        public byte[] outputSlots = null;
        public int[] analogArray = null;


        public JVS(string _Sp, Int32 _Baud = 115200, bool isHost = true, bool useCD = false, bool useDSR = false)
        {
            _comPortName = _Sp;
            _comPortBaud = _Baud;
            _useCD = useCD;
            _useDSR = useDSR;
            byte ioA;

            if (isHost)
            {
                ioA = JVS_HOST_ADDR;
            } else
            {
                ioA = 0xFE;
            }

            // MAke this better later
            thisNodeInfo = new JVSIOBoard(ioA, "Sharp JVS for Windows;Naokis Retro Corner;V0.1", 17, 17, 17, new List<Feature> { });
        }

        public async Task<int> MainAsync()
        {
            int errorCode = 0;

            await Task.Run(() =>
            {
                Connect();
                while (_isRunning)
                {
                    // JVS CRAP
                }
            });

            return errorCode;
        }


        public int Main()
        {
            int errorCode = 0;

            // Init IO Boards
            int _ioCount = Connect();
            if (_ioCount > 0x20) { return _ioCount; }

            // Get all IO Board specs
            if (_ioCount == 0)
            {
                Console.WriteLine("JVS: No IO Boards found.");
            }
            else
            {
                Console.WriteLine("Found: ");
            }
            for (int i = 1; i <= _ioCount; i++)
            {
                for (int t = 0; t < 3; t++)
                {
                    JVSIOBoard board = GetIOMetaDeta(_ioCount);
                    if (board != null)
                    {
                        _boards.Add(board);
                        break;
                    }
                }

                Console.WriteLine(
                    _boards[i - 1].NodeID.ToString() + ": " +
                    _boards[i - 1].Identity
                    );
            }

            /*while (_isRunning)
            {
                // JVS CRAP
            }*/

            return errorCode;
        }

        public JVSIOBoard GetIOMetaDeta(int _id)
        {
            string ioName = "";
            int ioCmd = 0;
            int ioJVS = 0;
            int ioCom = 0;
            List<Feature> ioFeat = new List<Feature> { };

            // Get IO board name
            RequestIoIdentity((byte)_id);
            JVS_Frame _reply = Read();
            if (_reply != null)
            {
                ioName = Encoding.ASCII.GetString(_reply.data.ToArray(), 1, _reply.data.Count() - 1);
            }

            RequestVersions((byte)_id);
            _reply = Read();
            if (_reply != null)
            {
                ioCmd = _reply.data[1];
                ioJVS = _reply.data[3];
                ioCom = _reply.data[5];
                int f = 7;
                while(f < _reply.data.Count())
                {
                    byte fType = _reply.data[f++];
                    if (fType == JVS_FEATURE_END)
                    {
                        break;
                    }
                    byte fP1 = _reply.data[f++];
                    byte fP2 = _reply.data[f++];
                    byte fP3 = _reply.data[f];
                    ioFeat.Add(new Feature(fType, new byte[] { fP1, fP2, fP3 }));
                    f++;
                }

            }
            return new JVSIOBoard((byte)_id, ioName, ioCom, ioJVS, ioCmd, ioFeat);
        }

        public void Reset()
        {
            Console.WriteLine("JVS: Send reset.");
            JVS_Frame _frame = new JVS_Frame(JVS_BROADCAST_ADDR, new List<byte> { JVS_RESET_CODE, 0xD9 });
            Write(_frame);
            Thread.Sleep(10);
            Write(_frame);
        }

        public int Connect()
        {
            int _ioCount = 0;

            if (_comPortName == null)
            {
                throw new Exception("Serial port name is NULL.");
            }

            _comPort = new SerialPort(_comPortName, _comPortBaud);
            Console.WriteLine("Connecting to " + _comPortName + ":");

            try
            {
                // Open the serial port
                _comPort.Open();
                Console.WriteLine(_comPortName + " opened at " + _comPortBaud.ToString() + ".");
                Reset();

                int _addrErr = 0;
                bool _addrDone = false;

                if (_useCD || _useDSR)
                {
                    _addrDone = ReadSense() > 0;
                }

                while (_ioCount < 15 && !_addrDone && _addrErr < 3)
                {
                    if (_useCD || _useDSR)
                    {
                        _addrDone = ReadSense() > 0;
                    } else
                    {
                        // No way to check for more boards using the standard, assume only one is needed.
                        _addrDone = true;
                    }

                    JVS_Frame _idF = new JVS_Frame((byte)(JVS_BROADCAST_ADDR), new List<byte> { JVS_SETADDR_CODE, (byte)(_ioCount + 1) });
                    Write(_idF);

                    JVS_Frame _ioR = Read();
                    if (_ioR == null) { _addrErr++; }
                    else
                    {
                        if (_ioR.statusCode == JVS_STATUS_NORMAL && _ioR.data[0] == JVS_REPORT_NORMAL)
                        {
                            _ioCount++;
                        }
                        else
                        {
                            Console.WriteLine("JVS: IO " + _ioCount + " Reported Error: SC: " + _ioR.statusCode.ToString() + ", R: " + _ioR.data[0]);
                            _addrErr++;
                        }

                    }
                }
                if (_addrErr == 3)
                {
                    Console.WriteLine("JVS: Error setting address ID: " + (_ioCount + 1).ToString());
                    _ioCount += 0x21;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            return _ioCount;
        }

        public void Close()
        {
            // Disconnect from serial
            _isRunning = false;
            _comPort.Close();
        }

        public int ReadSense()
        {
            if (_useCD)
            {
                if (_comPort.CDHolding)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            else if (_useDSR)
            {
                if (_comPort.DsrHolding)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            else return 1;
        }

        private int Resend()
        {
            Console.WriteLine("JVS: Resend last packet");
            return Write(TXBuffer);
        }

        public int Write(JVS_Frame _f)
        {
            int _eC = 0;
            byte[] data = _f.ToBytes();
            try
            {
                _comPort.Write(data, 0, data.Length);
                TXBuffer = _f;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            return _eC;
        }

        public JVS_Frame Read()
        {
            if (WaitForBytes(5) != 0) { return null; }

            List<byte> _d = new List<byte> { };

            int _errCode = 0;
            bool _fDone = false;
            int _fIndex = 0;
            int _fStep = 0;

            while (!_fDone && _errCode == 0)
            {
                _d.Insert(_fIndex, (byte)_comPort.ReadByte());
                switch (_fStep)
                {
                    default:
                    case 0:
                        // JVS Sync
                        if (_d[_fIndex++] == JVS_SYNC)
                        {
                            _fStep++;
                        }
                        break;
                    case 1:
                        // JVS ID
                        if (_d[_fIndex++] == thisNodeID)
                        {
                            _fStep++;
                        }
                        else
                        {
                            return null;
                        }
                        break;
                    case 2:
                        // JVS Number of bytes
                        byte x = _d[_fIndex++];
                        if (_comPort.BytesToRead < x)
                        {
                            if (WaitForBytes(x) != 0)
                            {
                                // Prevent timeout whilst waiting for rest of packet
                                Console.WriteLine("JVS: Timeout whilst waiting for remainder of packet.");
                                return null;

                            }
                        }

                        // All bytes are in buffer?
                        while (_comPort.BytesToRead > 0)
                        {
                            _d.Add((byte)_comPort.ReadByte());
                        }
                        _fDone = true;
                        break;
                }
            }

            JVS_Frame _f = new JVS_Frame(_d.ToArray());
            switch (_f.statusCode)
            {
                case JVS_STATUS_NORMAL: break;
                case JVS_STATUS_CHECKSUMERROR: Resend(); break;
                case JVS_STATUS_OVERFLOW: _errCode = 11; break;
                case JVS_STATUS_UNKNOWNCMD:
                    Console.WriteLine("JVS: IO Reported unsupported command");
                    string hex = BitConverter.ToString(_f.data.ToArray());
                    Console.WriteLine("\t" + hex);
                    break;
            }

            if (_errCode != 0) { return null; }

            return _f;
        }

        public int WaitForBytes(int _nB)
        {
            int _timeOut = 0;
            while (_comPort.BytesToRead < _nB)
            {
                // Wait for bytes to arrive
                Thread.Sleep(5);
                _timeOut++;
                if (_timeOut > 40)
                {
                    return 1;
                }
            }
            return 0;
        }

        // Set the node ID. 0x00 is reserved for the host, 0xFF for broadcast packets
        public int SetID(int id)
        {
            if (id != JVS_HOST_ADDR && id != JVS_BROADCAST_ADDR)
            {
                thisNodeID = (byte)id;
                //jvsReady = true;
                SetSense(JVS_SENSE_ACTIVE);
                //sendReport(JVS_STATUS_NORMAL, JVS_REPORT_NORMAL);
                return 0;
            }
            return id;
        }

        public bool Update()
        {
            if (_comPort.BytesToRead >= 5) RXBuffer = Read();
            return _rxFlag;
        }

        public int RunCommand()
        {
            //rxFlag = false;
            return RunCommand(RXBuffer);
        }

        public int RunCommand(JVS_Frame _RX)
        {
            if (thisNodeInfo.NodeID != JVS_HOST_ADDR)
            {
                Console.WriteLine("JVS: RunCommand only works in device mode!");
                return 1;
            }

            if (thisNodeInfo == null)
            {
                Console.WriteLine("JVS Error: Info array not set, runCommand would likely fail!");
                return 2;
            }

            int errorCode = 0;
            // Read the command byte
            int _idx = 0;      // Temp int
            byte statusCode = JVS_STATUS_NORMAL;
            byte[] data = new byte[] { };
            bool assignID = false;     // To specify whether to take the given ID or wait for downstream ports

            bool responseNeeded = true;

            for (int c = 0; c < _RX.numBytes - 1; c++)
            {
                switch (_RX.data[c])
                {
                    // Defaults to doing nothing, user to respond
                    case JVS_RESET_CODE:
                        c++;
                        if (_RX.data[c] == 0xD9)
                        {
                            Console.WriteLine("JVS Recieved: Reset");
                            ResetNode();
                            responseNeeded = false;
                        }
                        break;

                    case JVS_SETADDR_CODE:

                        c++;    // Get next data byte
                        if (ReadSense() > 0)
                        {
                            // Is downstream port reporting an ID (if one exists)
                            assignID = true;
                            //Console.WriteLine(assignID ? "JVS: AssignID: True" : "JVS: assignID: False");
                        }
                        if (assignID)
                        {
                            if (SetID(_RX.data[c]) == 254)
                            {
                                Console.WriteLine("JVS Recieved: Couldn't set ID");
                            }
                            else
                            {
                                responseNeeded = true;
                                statusCode = JVS_STATUS_NORMAL;
                                data.Append(JVS_REPORT_NORMAL);
                            }
                        }
                        else
                        {
                            responseNeeded = false;
                        }
                        c++;
                        break;

                    case JVS_IOIDENT_CODE:
                        // IO ident
                        Console.WriteLine("JVS Recieved: Identify");
                        responseNeeded = true;
                        data.Append(JVS_REPORT_NORMAL);
                        for (int s = 0; s < 99; s++)
                        {
                            if (thisNodeInfo.Identity[s] != 0)
                            {
                                data.Append((byte)thisNodeInfo.Identity[s]);
                            }
                            else
                            {
                                data.Append((byte)0);
                                break;
                            }
                        }
                        data.Append((byte)0);
                        break;

                    case JVS_CMDREV_CODE:
                        // CMD Rev
                        Console.WriteLine("JVS Recieved: Report Command version.");
                        responseNeeded = true;
                        data.Append(JVS_REPORT_NORMAL);
                        data.Append(DEC2BCD((byte)thisNodeInfo.CmdVersion));
                        break;

                    case JVS_JVSREV_CODE:
                        Console.WriteLine("JVS Recieved: Report JVS version.");
                        // JVS Rev
                        responseNeeded = true;
                        data.Append(JVS_REPORT_NORMAL);
                        data.Append(DEC2BCD((byte)thisNodeInfo.JVSVersion));
                        break;

                    case JVS_COMVER_CODE:
                        Console.WriteLine("JVS Recieved: Report Comms. version.");
                        // Comm Rev
                        responseNeeded = true;
                        data.Append(JVS_REPORT_NORMAL);
                        data.Append(DEC2BCD((byte)thisNodeInfo.CommVersion));
                        break;

                    case JVS_FEATCHK_CODE:
                        Console.WriteLine("JVS Recieved: Report supported features.");
                        // Feature support
                        // For some reason the english translation doesn't mention the feature codes are in BCD
                        responseNeeded = true;
                        data.Append(JVS_REPORT_NORMAL);
                        for (int f = 0; f < thisNodeInfo.Features.Count(); f++)
                        {
                            data.Append(DEC2BCD((byte)thisNodeInfo.Features[f].FeatureType)); // Type
                            data.Append(DEC2BCD((byte)thisNodeInfo.Features[f].FeatureAtrributes[0])); // Param 0
                            data.Append(DEC2BCD((byte)thisNodeInfo.Features[f].FeatureAtrributes[1])); // Param 1
                            data.Append(DEC2BCD((byte)thisNodeInfo.Features[f].FeatureAtrributes[2])); // Param 2
                        }
                        data.Append((byte)JVS_FEATURE_END);
                        break;

                    case JVS_MAINID_CODE:
                        // Main board ident, send ack
                        Console.WriteLine("JVS Recieved: Main board ID (Currently Unsupported)");
                        responseNeeded = true;
                        statusCode = JVS_STATUS_UNKNOWNCMD;
                        break;

                    case JVS_READSWITCH_CODE:
                        // Read switch inputs
                        if (playerArray == null)
                        {
                            Console.WriteLine("JVS Error: Host requested switches, player array is NULL!");
                            return -3;
                        }
                        responseNeeded = true;

                        c++;                        // Get next data byte
                        byte p = _RX.data[c++];  // How many players to read
                        if (p > thisNodeInfo.Features.Find(f => f.FeatureType == JVS_FEATURE_SWITCH).FeatureAtrributes[0])
                        {
                            errorCode = 1;
                        }

                        byte b = _RX.data[c];  // How many bytes per player
                        if (b > (1 * thisNodeInfo.Features.Find(f => f.FeatureType == JVS_FEATURE_SWITCH).FeatureAtrributes[1]))
                        {
                            errorCode = 1;
                        }
                        else
                        {
                            data.Append(JVS_REPORT_NORMAL); // Byte 0 report
                            data.Append(machineSwitches);   // Byte 1 Test SW, TILT
                            for (byte pC = 0; pC < p; pC++)
                            {
                                // Player number
                                byte bC = (byte)(b * pC);
                                for (byte bT = 0; bT < b; bT++)
                                {
                                    // Byte number
                                    data.Append(playerArray[b + bC]);
                                }
                            }
                            errorCode = 0;
                        }

                        if (errorCode > 0)
                        {
                            // If host request more players or buttons than supported, send error
                            Console.WriteLine("JVS Error: Parameter error on switch request command");
                            data.Initialize();
                            data.Append(JVS_REPORT_PARAMETERERROR);
                            errorCode = 0;
                        }
                        break;

                    case JVS_READCOIN_CODE:
                        // Read coin inputs
                        if (coinSlots == null || coinCondition == null)
                        {
                            Console.WriteLine("JVS Error: Host requested coins, coin slots/condition array is NULL!");
                            return -4;
                        }
                        responseNeeded = true;
                        c++;    // Get next parameter byte
                                // If host requests 2 players and IO supports 2, p = 0. 
                                // Host should not normally request more than what IO supports
                        if (thisNodeInfo.Features.Find(f => f.FeatureType == JVS_FEATURE_COIN).FeatureAtrributes[0] - _RX.data[c] < 0)
                        {
                            // If host request more players than supported, send error
                            data.Append(JVS_REPORT_PARAMETERERROR);
                        }
                        else
                        {
                            byte pC = _RX.data[c];  // How many players to read

                            data.Append(JVS_REPORT_NORMAL); // Byte 0 report

                            for (int pS = 0; pS < pC; pS++)
                            {
                                // Coin slot condition
                                byte cS = (JVS_COIN_NORMAL << 6);
                                if (coinCondition != null)
                                {
                                    c = (coinCondition[pS] << 6);
                                }
                                // Coin slot count
                                if (coinSlots != null)
                                {
                                    data.Append((byte)(cS | ((coinSlots[pS] >> 8 & 0xFF) & 0x3F)));
                                    data.Append((byte)(coinSlots[pS] & 0xFF));
                                }
                                else
                                {
                                    data.Append((byte)0);
                                    data.Append((byte)0);
                                }
                            }
                        }
                        break;
                    case JVS_COINDECREASE_CODE:
                        // Decrease coin counter (the credit count, NOT the coin counter of the cabinet)
                        if (coinSlots == null || coinCondition == null)
                        {
                            Console.WriteLine("JVS Error: Host requested decreaese of coins, coin slots/condition array is NULL!");
                            return -4;
                        }
                        responseNeeded = true;
                        c++;
                        _idx = _RX.data[c++];
                        if (_idx > thisNodeInfo.Features.Find(f => f.FeatureType == JVS_FEATURE_COIN).FeatureAtrributes[0])
                        {
                            // Check the slots
                            data.Append(JVS_REPORT_PARAMETERERROR);
                        }
                        else if ((_RX.numBytes - 1) < 2)
                        {
                            // Not enough data in packet to fufill command
                            data.Append(JVS_REPORT_DATAERROR);
                        }
                        else
                        {
                            data.Append(JVS_REPORT_NORMAL);
                            int tmp = (_RX.data[c++] << 8);
                            tmp |= _RX.data[c];
                            if (tmp > coinSlots[_idx - 1])
                            {
                                coinSlots[_idx - 1] = 0;
                            }
                            else
                            {
                                coinSlots[_idx - 1] -= tmp;
                            }
                            Console.WriteLine("JVS: Coin " + _idx + ": " + coinSlots[_idx - 1] + " - " + tmp);
                        }
                        break;

                    case JVS_COININCREASE_CODE:
                        // Increase coin counter (the credit count, NOT the coin counter of the cabinet)
                        if (coinSlots == null || coinCondition == null)
                        {
                            Console.WriteLine("JVS Error: Host requested increase in coins, coin slots/condition array is NULL!");
                            return -4;
                        }
                        responseNeeded = true;
                        c++;
                        _idx = _RX.data[c++];
                        if (_idx > thisNodeInfo.Features.Find(f => f.FeatureType == JVS_FEATURE_COIN).FeatureAtrributes[0])
                        {
                            // Check the slots
                            data.Append(JVS_REPORT_PARAMETERERROR);
                        }
                        else if ((_RX.numBytes - 1) < 2)
                        {
                            // Not enough data in packet to fufill command
                            data.Append(JVS_REPORT_DATAERROR);
                        }
                        else
                        {
                            data.Append(JVS_REPORT_NORMAL);
                            byte tmp = (byte)(_RX.data[c++] << 8);
                            tmp |= _RX.data[c];
                            coinSlots[_idx - 1] += tmp;
                            Console.WriteLine("JVS: Coin " + _idx + ": " + coinSlots[_idx - 1] + " - " + tmp);
                        }
                        break;

                    case JVS_GENERICOUT1_CODE:
                        // GPO 1
                        if (outputSlots == null)
                        {
                            Console.WriteLine("JVS Error: Host wrote to output, but output array is NULL!");
                            return -5;
                        }
                        responseNeeded = true;
                        c++;
                        byte temp = _RX.data[c++];
                        byte tempB = 0;

                        for (int x = 0; x < (thisNodeInfo.Features.Find(f => f.FeatureType == JVS_FEATURE_GPO).FeatureAtrributes[0]); x += 8)
                        {
                            tempB++;
                        }
                        if ((temp - tempB) < 0)
                        {
                            // If host request more bytes than supported, send error
                            data.Append(JVS_REPORT_PARAMETERERROR);
                        }
                        else
                        {
                            data.Append(JVS_REPORT_NORMAL);
                            for (int o = 0; o < temp; o++)
                            {
                                outputSlots[o] = _RX.data[c++];
                            }
                        }
                        break;
                    case JVS_DATARETRY_CODE:
                        // Host was not happy with the packet, resend
                        responseNeeded = false; // We need to send a specific packet back
                        Resend();
                        break;
                    // Default case is to send an unknown command response.
                    // Any commands executed before this will still send
                    // Any commands after this will be not be ran.
                    default:
                    case 0x02:
                        // Unknown
                        Console.WriteLine("JVS Warning: Host sent an unkown command: 0x" + _RX.data[c]);
                        responseNeeded = true;
                        statusCode = JVS_STATUS_UNKNOWNCMD;
                        c += _RX.numBytes - 1;
                        errorCode = 12;
                        break;
                }
            }

            if (responseNeeded == true)
            {
                List<byte> dList = new List<byte>();
                dList.AddRange(data);
                JVS_Frame _frame = new JVS_Frame(JVS_HOST_ADDR, dList, false, statusCode);
                Write(_frame);
            }
            return errorCode;
        }

        public bool Available()
        {
            if (_comPort.BytesToRead >= 5)
            {
                RXBuffer = Read();
                if (RXBuffer != null) { _rxFlag = true; }
            }
            return _rxFlag;
        }

        public void ResetNode()
        {
            if (thisNodeInfo.NodeID != JVS_HOST_ADDR)
            {
                SetSense(JVS_SENSE_INACTIVE);         // High means ID not assigned
                thisNodeInfo.NodeID = 0xFE;
                // If we were handling other things like analog outputs, we would need to clear those too.
                if (outputSlots != null)
                {
                    byte tempB = 0;
                    for (int x = 0; x < thisNodeInfo.Features.Find(f => f.FeatureType == JVS_FEATURE_GPO).FeatureAtrributes[0]; x += 8)
                    {
                        // Count up how many bytes are needed for given parameter
                        tempB++;
                    }
                    outputSlots.Initialize();
                }
            }
        }

        // Sets the JVS sense line. Send HIGH to set sense to 5V
        public void SetSense(bool s)
        {
            Console.WriteLine("JVS: Set sense pin: " + (s ? "0V" : "2.5V"));
            if (_useDTR)
            {
                _comPort.DtrEnable = s;
            }
        }


        /* Host mode JVS commands */

        public void WriteOutputs(byte id)
        {
            // Write outputSlots array
            // Only use for IO boards with more than 8 GPOs
            WriteOutputs(id, outputSlots);
        }

        public void WriteOutputs(byte id, byte[] data)
        {
            // GPO1
            // Writes output byte array from GPO 0 to X
            // Only use for IO boards with more than 8 GPOs
            Console.WriteLine("JVS: Write GPO1");

            if (thisNodeInfo.NodeID == JVS_HOST_ADDR)
            {
                List<byte> dataList = new List<byte> { };
                dataList.AddRange(data);
                JVS_Frame frame = new JVS_Frame(id, dataList);
                Write(frame);
            }
        }

        public void WriteOutputByte(byte id, byte idx, byte data)
        {
            // GPO2
            // Writes output byte array from GPO 0 to X
            // Not all IO boards support this command

            Console.WriteLine("JVS: Write GPO2");

            if (thisNodeInfo.NodeID == JVS_HOST_ADDR)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_GENERICOUT2_CODE, idx, data });
                Write(frame);
            }
        }

        public void WriteOutputBit(byte id, byte idx, bool data)
        {
            // GPO2
            // Writes output byte array from GPO 0 to X
            // Not all IO boards support this command

            Console.WriteLine("JVS: Write GPO3");

            if (thisNodeInfo.NodeID == JVS_HOST_ADDR)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_GENERICOUT3_CODE, idx, (data ? (byte)0x01 : (byte)0x00) });
                Write(frame);
            }
        }

        public void RequestIoIdentity(byte id)
        {
            // Send IO Identify command to ID
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_IOIDENT_CODE });
                Write(frame);
            }
        }

        public void RequestVersions(byte id)
        {
            // Send version request as concatenated packet
            // Read reply in order of CMD Rev, JVS Rev, COM Version and then supported features
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_CMDREV_CODE, JVS_JVSREV_CODE, JVS_COMVER_CODE, JVS_FEATCHK_CODE });
                Write(frame);
            }
        }

        public void WriteMainID(byte id)
        {
            // Write the ID string located in _info under mainID
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR)
            {
                List<byte> data = new List<byte> { };
                data.AddRange(Encoding.ASCII.GetBytes(thisNodeInfo.Identity));
                JVS_Frame frame = new JVS_Frame(id, data);
                Write(frame);
            }
        }

        public void ReadSwitches(byte id, byte p, byte d)
        {
            // Request switch data from ID. P = How many players, D = how many bytes per player
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && p > 0 && d > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_READSWITCH_CODE, p, d });
                Write(frame);
            }
        }

        public void ReadCoins(byte id, byte c)
        {
            // Request coin data from ID. C = How many coin slots to read
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_READCOIN_CODE, c });
                Write(frame);
            }
        }

        public void ReadAnalog(byte id, byte c)
        {
            // Request analog data from ID. C = How many channels to read
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_READANALOG_CODE, c });
                Write(frame);
            }
        }

        public void WriteAnalog(byte id, byte c, int d)
        {
            // Write analog data to ID.
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_ANALOGOUT_CODE, c, (byte)(d >> 8 & 0xff), (byte)(d & 0xFF) });
                Write(frame);
            }
        }

        public void WriteCharacter(byte id, byte d)
        {
            // Request misc. switch data from ID. B = how many bytes to read
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && d > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_CHARACTEROUT_CODE, 1, d });
                Write(frame);
            }
        }

        public void ReadRotary(byte id, byte c)
        {
            // Request rotary data from ID. C = How many channels to read
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_READROTARY_CODE, c });
                Write(frame);
            }
        }

        public void ReadKeypad(byte id)
        {
            // Request keypad data from ID.
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_READKEY_CODE });
                Write(frame);
            }
        }

        public void ReadSceenPos(byte id, byte c)
        {
            // Request touch screen position data from ID. C = Channel index
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_READSCREENPOS_CODE, c });
                Write(frame);
            }
        }

        public void ReadMiscSwitch(byte id, byte b)
        {
            // Request misc. switch data from ID. B = how many bytes to read
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && b > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_READMISC_CODE, b });
                Write(frame);
            }
        }

        public void ReadPayout(byte id, byte c)
        {
            // Request payout hopper data from ID. c = Which channel to read
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_READPAYOUT_CODE, c });
                Write(frame);
            }
        }

        public void RequestRetransmit(byte id)
        {
            // Request retransmit of last packet
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_DATARETRY_CODE });
                Write(frame);
            }
        }

        public void IncreaseCoin(byte id, byte s, int c)
        {
            // Increment coin counter amount on IO. S = Which slot, C = Amount
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && s > 0 && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_COININCREASE_CODE, s, (byte)(c >> 8 & 0xFF), (byte)(c & 0xFF) });
                Write(frame);
            }
        }

        public void DecreaseCoin(byte id, byte s, int c)
        {
            // Decrement coin counter amount on IO. S = Which slot, C = Amount
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && s > 0 && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_COININCREASE_CODE, s, (byte)(c >> 8 & 0xFF), (byte)(c & 0xFF) });
                Write(frame);
            }
        }

        public void IncreasePayout(byte id, byte s, int c)
        {
            // Increase payout amount for IO to issue. S = Which slot, C = Amount
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && s > 0 && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_PAYOUTINCREASE_CODE, s, (byte)(c >> 8 & 0xFF), (byte)(c & 0xFF) });
                Write(frame);
            }
        }

        public void DecreasePayout(byte id, byte s, int c)
        {
            // Increase payout amount for IO to issue. S = Which slot, C = Amount
            if (thisNodeInfo.NodeID == JVS_HOST_ADDR && s > 0 && c > 0)
            {
                JVS_Frame frame = new JVS_Frame(id, new List<byte> { JVS_PAYOUTDECREASE_CODE, s, (byte)(c >> 8 & 0xFF), (byte)(c & 0xFF) });
                Write(frame);
            }
        }

        public byte DEC2BCD(byte dec) { return (byte)(((dec / 10) << 4) + (dec % 10)); }
        public byte BCD2DEC(byte bcd) { return (byte)(((bcd >> 4) * 10) + (bcd & 0xf)); }
    }
}

namespace PSJVS.Containers
{
    enum FeatureTypes
    {
        endCode, switchInput, coinInput, analogInput, rotaryInput, keycodeInput,
        screenPosInput, miscInput, reserved1, reserved2, cardSlots, medalOutputs,
        gpOutput, analogOutput, characterOutput, backupSupport
    }

    enum CharacterOutputType
    {
        unknown, asciiNumeric, asciiAlpha, asciiKatakana, asciiKanji
    }

    public class JVSIOBoard
    {
        public byte NodeID;
        public string Identity;
        public int CommVersion;
        public int JVSVersion;
        public int CmdVersion;
        public List<Feature> Features;

        public JVSIOBoard(byte _Id, string _N, int _CoV, int _JV, int _CmV, List<Feature> _f)
        {
            NodeID = _Id;
            Identity = _N;
            CommVersion = _CoV;
            JVSVersion = _JV;
            CmdVersion = _CmV;
            Features = _f;
        }
    }

    public class Feature
    {
        public int FeatureType;
        public byte[] FeatureAtrributes = new byte[3];

        public Feature(int _Ft, byte[] _Fa)
        {
            if (_Fa.Length != 3 || _Fa == null)
            {
                throw new ArgumentException("Feature attributes array must not be null and must have a length of 3.");
            }
            if (_Ft == 0)
            {
                throw new ArgumentException("Feature type cannot be 0.");
            }

            // Initialize the array elements
            FeatureType = _Ft;
            for (int i = 0; i < FeatureAtrributes.Length; i++)
            {
                FeatureAtrributes[i] = _Fa[i]; // Example initialization
            }
        }

        // Returns the given feature as a list of bytes
        public List<byte> FeatureToList(Feature _f)
        {
            List<byte> list = new List<byte>
            {
                (byte)_f.FeatureType,
                _f.FeatureAtrributes[0],
                _f.FeatureAtrributes[1],
                _f.FeatureAtrributes[2]
            };
            return list;
        }

        // Returns a list of features as a list of bytes
        public List<byte> FeatureListToList(Feature[] _f)
        {
            List<byte> list = new List<byte>();
            for (int i = 0; i < _f.Count(); i++)
            {
                list.Concat(FeatureToList(_f[i]));
            }
            return list;
        }
    }

    public class JVS_Frame
    {
        private const byte JVS_SYNC = 0xE0;
        private const byte JVS_MARK = 0xD0;
        public byte nodeID = 0;
        public byte numBytes = 0;          // Includes all data bytes and sync
        public byte statusCode = 1;
        public List<byte> data = new List<byte> { };
        public byte sum = 0;                // Checksum of ID, numbytes, data bytes
        public List<byte> rawPacket = new List<byte> { };

        public JVS_Frame(byte[] _d, bool isHost = true)
        {
            int idx = 1; // Ignore sync here

            rawPacket = new List<byte>(_d);
            // Find all occurrences of = 0xE0 in the list
            for (int i = 0; i < rawPacket.Count; i++)
            {
                if ((rawPacket[i] == JVS_MARK))
                {
                    // If any byte is MARK, then the following byte was escaped
                    rawPacket.RemoveAt(i);
                    rawPacket[i]++;
                }
            }
            nodeID = rawPacket[idx++];
            numBytes = rawPacket[idx++];
            if (isHost)
            {
                statusCode = rawPacket[idx++];
                for (int i = 0; i < numBytes-2; i++)
                {
                   data.Add(rawPacket[idx++]);
                }
            }
            sum = rawPacket[idx++];
        }

        public JVS_Frame(byte _id, List<byte> _data, bool isHost = true, byte _Sc = 1)
        {
            nodeID = _id;
            data = _data;
            statusCode = _Sc;
            numBytes = (byte)data.Count();
            byte packetBytes = 0;

            if (isHost) packetBytes++;

            rawPacket.Add(JVS_SYNC);
            rawPacket.Add(_id);
            rawPacket.Add((byte)(data.Count + packetBytes));
            rawPacket.AddRange(data);
            CalculateSum(isHost, true);
            rawPacket.Add(sum);

            // Find all occurrences of SYNC or MARK in the list
            for (int i = 0; i < rawPacket.Count(); i++)
            {
                if ((rawPacket[i] == JVS_SYNC || rawPacket[i] == JVS_MARK) && i > 0)
                {
                    // If any byte other than the first is SYNC or MARK, then it needs a MARK
                    rawPacket.Insert(i, JVS_MARK);
                    rawPacket[i + 1]--;
                }
            }
        }

        public byte[] ToBytes()
        {
            byte[] byteList = rawPacket.ToArray();
            return byteList;
        }

        public byte CalculateSum(bool isHost = true, bool send = false)
        {
            UInt32 _s = 0;
            _s += nodeID;
            _s += (byte)(numBytes + 1);
            if ((!isHost && send) || (isHost && !send))
            {
                _s += statusCode;
            }

            for (int s = 0; s < numBytes; s++)
            {
                _s += data[s];
            }
            _s %= 256;
            sum = (byte)_s;
            return sum;
        }
    }

}

