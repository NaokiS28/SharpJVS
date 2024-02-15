using PSJVS.Containers;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Text;
using System.Globalization;


namespace SharpJVS
{
    public class JVS
    {
        public const byte BUFFER_FULL = 1;
        public const byte BUFFER_ADD_ERROR = 2;
        public const byte BUFFER_READ_ERROR = 3;
        public const byte JVS_BUFFER_SIZE = 1;

        public const byte JVS_SENSE_INACTIVE = 0;
        public const byte JVS_SENSE_ACTIVE = 1;

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
        private string _comPortName;
        private Int32 _comPortBaud;
        private SerialPort _comPort;
        private bool _useDSR = false;
        private bool _useCD = false;

        private byte thisNodeID = JVS_HOST_ADDR;
        private JVS_Frame LastSent = null;

        public JVS(string _Sp, Int32 _Baud = 115200, bool useCD = false, bool useDSR = false)
        {
            _comPortName = _Sp;
            _comPortBaud = _Baud;
            _useCD = useCD;
            _useDSR = useDSR;
        }

        /*
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
        */

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
            JVS_Frame _frame = new JVS_Frame((byte)_id, new List<byte> { JVS_IOIDENT_CODE });
            Write(_frame);
            JVS_Frame _reply = Read();
            if (_reply != null)
            {
                ioName = System.Text.Encoding.ASCII.GetString(_reply.data.ToArray(),1,_reply.data.Count()-1);
            }

            _frame = new JVS_Frame((byte)_id, new List<byte> { JVS_CMDREV_CODE, JVS_JVSREV_CODE, JVS_COMVER_CODE, JVS_FEATCHK_CODE });
            Write(_frame);
            _reply = Read();
            if (_reply != null)
            {
                ioCmd = _reply.data[1];
                ioJVS = _reply.data[3];
                ioCom = _reply.data[5];
                bool _featDone = false;
                for (int f = 7; !_featDone; f++)
                {
                    byte fType = _reply.data[f++];
                    if (fType == JVS_FEATURE_END)
                    {
                        _featDone = true;
                        break;
                    }
                    byte fP1 = _reply.data[f++];
                    byte fP2 = _reply.data[f++];
                    byte fP3 = _reply.data[f];
                    ioFeat.Add(new Feature(fType, new byte[] { fP1, fP2, fP3 }));
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

        private int Connect()
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
            return Write(LastSent);
        }

        private int Write(JVS_Frame _f)
        {
            int _eC = 0;
            byte[] data = _f.ToBytes();
            try
            {
                _comPort.Write(data, 0, data.Length);
                LastSent = _f;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            return _eC;
        }

        private JVS_Frame Read()
        {
            if (waitForBytes(5) != 0) { return null; }

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
                            if (waitForBytes(x) != 0)
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
                case JVS_STATUS_NORMAL: _fStep++; break;
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

        private int waitForBytes(int _nB)
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
            List<byte> list = new List<byte>();
            list.Add((byte)FeatureType);
            list.Add(FeatureAtrributes[0]);
            list.Add(FeatureAtrributes[1]);
            list.Add(FeatureAtrributes[2]);
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

    class JVS_Frame
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
            calculateSum(isHost, true);
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

        public byte calculateSum(bool isHost = true, bool send = false)
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

