using System.Collections;
using System.Text;

namespace VirtualMachine
{
    internal class Program
    {
        static void Main(string[] args)
        {
            VirtualMachine vm = new VirtualMachine();

            string codeStr = """
                             mov r0 %0
                             mov r1 15
                             mov r2 8

                             mov r3 25928
                             mov r4 %1
                             str r3 r4

                             mov r3 27756
                             mov r4 %2
                             str r3 r4

                             mov r3 8303
                             mov r4 %3
                             str r3 r4

                             mov r3 28535
                             mov r4 %4
                             str r3 r4

                             mov r3 27762
                             mov r4 %5
                             str r3 r4

                             mov r3 100
                             mov r4 %6
                             str r3 r4

                             sav r5
                             add r1 r1 r2
                             cmp r1 r0
                             jmple r5
                             str r1 r0

                             mov r7 %0
                             mov r8 4
                             mov r0 0
                             sav r6
                             add r7 r7 r8
                             ldr r9 r7
                             prn r9
                             cmp r0 r9
                             jmpne r6
                             """;

            uint[] code = AssToByteCode(codeStr);

            vm.SetMemory(code);
            vm.Run();
        }


        public static uint[] AssToByteCode(string assCode)
        {
            string[] lines = assCode.Split('\n');
            lines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            List<uint> bytes = [];
            foreach (string line in lines)
            {
                var inputs = line.Split(' ');
                string opStr = inputs[0];
                uint cond = 0;
                if (inputs[0].Length > 3)
                {
                    opStr = inputs[0][..3];
                    string condStr = inputs[0][3..];
                    if (condStr.Contains('l'))
                    {
                        cond |= 0b001_00000000000000000000000000000;
                    }

                    if (condStr.Contains('g'))
                    {
                        cond |= 0b010_00000000000000000000000000000;
                    }

                    if (condStr.Contains("ne"))
                    {
                        cond |= 0b011_00000000000000000000000000000;
                    }
                    else if (condStr.Contains('e'))
                    {
                        cond |= 0b100_00000000000000000000000000000;
                    }
                }

                OpCode op = (OpCode)Enum.Parse(typeof(OpCode), opStr);

                uint c = cond ^ ((uint)op << 23);

                for (int i = 1; i < inputs.Length; i++)
                {
                    if (inputs[i].Contains('r'))
                    {
                        string reg = inputs[i].Replace('r', ' ');
                        uint par = Convert.ToUInt32(reg);
                        par <<= 19 - 4 * (i - 1);
                        c ^= par;
                    }
                    else if (inputs[i].Contains('%'))
                    {
                        string addr = inputs[i].Replace('%', ' ');
                        uint par = Convert.ToUInt32(addr) * 4 + (uint)lines.Length * 4 + 1;
                        c ^= par;
                    }
                    else
                    {
                        uint par = Convert.ToUInt32(inputs[i]);
                        c ^= par;
                    }
                }

                bytes.Add(c);
            }

            return bytes.ToArray();
        }
    }


    public enum OpCode
    {
        // Load from memory
        ldr = 0,

        // Store to memory
        str,
        add,
        sub,
        mul,
        div,
        mov,
        cmp,
        jmp,

        // Save next Address to register
        sav,

        // Print as ascii
        prn
    }

    public class VirtualMachine
    {
        private readonly BitArray _conditionFlags = new(3, false);

        public bool Equal
        {
            get { return _conditionFlags[0]; }
            private set { _conditionFlags[0] = value; }
        }

        public bool Greater
        {
            get { return _conditionFlags[1]; }
            private set { _conditionFlags[1] = value; }
        }

        public bool Less
        {
            get { return _conditionFlags[2]; }
            private set { _conditionFlags[2] = value; }
        }

        private uint[] _registers { get; set; } = new uint[16];

        private int _codeLength = 0;
        private byte[] _memory { get; set; } = new byte[2048];

        public int ProgramCounter
        {
            get { return Convert.ToInt32(_registers[15]); }
            private set { _registers[15] = Convert.ToUInt32(value); }
        }

        public void SetMemory(uint[] code)
        {
            _codeLength = code.Length * 4;
            for (int i = 0; i < code.Length; i++)
            {
                byte[] b = BitConverter.GetBytes(code[i]);
                b.CopyTo(_memory, i * 4);
            }
        }

        public void Run()
        {
            while (_codeLength > ProgramCounter)
            {
                uint mem = BitConverter.ToUInt32(_memory, ProgramCounter);
                OpCode op = GetOpcode(mem);
                int par = GetRegister(mem, 0);
                int par1 = GetRegister(mem, 1);
                int par2 = GetRegister(mem, 2);

                ProgramCounter += 4;

                if (ConditionSet(mem))
                {
                    switch (op)
                    {
                        case OpCode.ldr:
                        {
                            int dst = GetRegister(mem, 0);
                            int addr = GetRegister(mem, 1);

                            _registers[dst] = BitConverter.ToUInt32(_memory, (int)_registers[addr]);
                        }
                            break;
                        case OpCode.str:
                        {
                            int val = GetRegister(mem, 0);
                            int dst = GetRegister(mem, 1);

                            byte[] b = BitConverter.GetBytes(_registers[val]);
                            b.CopyTo(_memory, _registers[dst]);
                        }
                            break;
                        case OpCode.add:
                        {
                            int dst = GetRegister(mem, 0);
                            int reg1 = GetRegister(mem, 1);
                            int reg2 = GetRegister(mem, 2);

                            _registers[dst] = _registers[reg1] + _registers[reg2];
                        }
                            break;
                        case OpCode.sub:
                        {
                            int dst = GetRegister(mem, 0);
                            int reg1 = GetRegister(mem, 1);
                            int reg2 = GetRegister(mem, 2);

                            _registers[dst] = _registers[reg1] - _registers[reg2];
                        }
                            break;
                        case OpCode.mul:
                        {
                            int dst = GetRegister(mem, 0);
                            int reg1 = GetRegister(mem, 1);
                            int reg2 = GetRegister(mem, 2);

                            _registers[dst] = _registers[reg1] * _registers[reg2];
                        }
                            break;
                        case OpCode.div:
                        {
                            int dst = GetRegister(mem, 0);
                            int reg1 = GetRegister(mem, 1);
                            int reg2 = GetRegister(mem, 2);

                            _registers[dst] = _registers[reg1] / _registers[reg2];
                        }
                            break;
                        case OpCode.mov:
                        {
                            int dst = GetRegister(mem, 0);

                            int reg = GetRegister(mem, 1);
                            uint val = GetValue(mem, 1);

                            //if (reg == 0 && val != 0)
                            _registers[dst] = val;
                            //else
                            //    _registers[dst] = _registers[reg];
                        }
                            break;
                        case OpCode.cmp:
                        {
                            int reg1 = GetRegister(mem, 0);
                            int reg2 = GetRegister(mem, 1);
                            Equal = _registers[reg1] == _registers[reg2];
                            Greater = _registers[reg1] > _registers[reg2];
                            Less = _registers[reg1] < _registers[reg2];
                        }
                            break;
                        case OpCode.jmp:
                        {
                            int dst = GetRegister(mem, 0);
                            ProgramCounter = (int)_registers[dst];
                        }
                            break;
                        case OpCode.sav:
                        {
                            int dst = GetRegister(mem, 0);
                            _registers[dst] = (uint)ProgramCounter;
                        }
                            break;
                        case OpCode.prn:
                        {
                            int reg = GetRegister(mem, 0);
                            uint ascii = _registers[reg];
                            Console.Write(Encoding.ASCII.GetString(BitConverter.GetBytes(ascii)));
                        }
                            break;
                    }
                    //Console.WriteLine($"{op} {par} {par1} {par2}");
                }
            }

            Console.WriteLine();
        }

        private static uint GetValue(uint mem, int pos)
        {
            uint mask = Convert.ToUInt32(Math.Pow(2, 23 - pos * 4)) - 1;
            uint tmp = mem & mask;
            return tmp;
        }

        private bool ConditionSet(uint mem)
        {
            uint tmp = mem >> 29;
            uint cond = 0;

            if (Equal)
                cond += 1 << 2;
            if (Greater)
                cond += 1 << 1;
            if (Less)
                cond += 1;

            return (cond & tmp) > 0 || tmp == 0;
        }

        private static int GetRegister(uint mem, int par)
        {
            uint tmp = mem << 9;
            tmp <<= 4 * par;
            tmp >>= 32 - 4;
            return (int)tmp;
        }

        private static OpCode GetOpcode(uint mem)
        {
            return (OpCode)(mem << 3 >> 32 - 6);
        }
    }
}