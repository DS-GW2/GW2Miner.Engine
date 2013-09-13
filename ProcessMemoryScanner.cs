using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Globalization;

namespace GW2SessionKey
{
    class ProcessMemoryScanner
    {
        #region Global fields
        //Instance of ProcessMemoryReader class to be used to read the memory.
        ProcessMemoryReader reader;

        #endregion

        public ProcessMemoryScanner(Process process)
        {
            reader = new ProcessMemoryReader();
            reader.ReadProcess = process;
        }

        public Guid FindGuid(string szSearchPattern, UInt32 offset)
        {
            Guid retGuid = Guid.Empty;

            byte[] response = Search(szSearchPattern, offset, (uint)Marshal.SizeOf(typeof(Guid)));

            if (response != null)
            {
                retGuid = new Guid(response);
            }

            return retGuid;
        }

        public String FindString(string szSearchPattern, UInt32 offset, uint bytesToRead)
        {
            String retString = String.Empty;

            byte[] response = Search(szSearchPattern, offset, bytesToRead);

            if (response != null)
            {
                int count = Array.IndexOf<byte>(response, 0, 0);
                if (count < 0) count = response.Length;
                retString = Encoding.ASCII.GetString(response, 0, count);
            }

            return retString;
        }

        private byte[] Search(string szSearchPattern, UInt32 offset, uint bytesToRead)
        {
            ProcessMemoryReader.MEMORY_BASIC_INFORMATION info;
            byte[] ret = null;

            // Parse fingerprint
            UInt32 len = (UInt32)(szSearchPattern.Length / 2);
            ushort[] pPattern = new ushort[len];
            UInt32 lResult = 0, retAddress = 0;

            MakeSearchPattern(szSearchPattern, (ushort)szSearchPattern.Length, ref pPattern);

            reader.OpenProcess();

            uint bytesRead;
            int p = 0;
            for (info = reader.VirtualQueryEx((IntPtr)p, out bytesRead);
                bytesRead > 0;
                p += (int)info.RegionSize,
                    info = reader.VirtualQueryEx((IntPtr)p, out bytesRead))
            {
                if (info.State == (uint)ProcessMemoryReader.memState.MEM_COMMIT &&
                    (info.Type == (uint)ProcessMemoryReader.memType.MEM_IMAGE))
                {
                    int bytes_read;
                    byte[] buffer;

                    buffer = reader.ReadProcessMemory((IntPtr)p, info.RegionSize, out bytes_read);
                    lResult = PatternSearch(buffer, (uint)bytes_read, pPattern, len);

                    if (lResult > 0)
                    {   // Found it!
                        retAddress = BitConverter.ToUInt32(buffer, (int)(lResult + offset));

                        byte[] response = reader.ReadProcessMemory((IntPtr)retAddress, bytesToRead, out bytes_read);
                        ret = response;
                        break;
                    }
                }
            }

            reader.CloseHandle();

            return ret;
        }

        //////////////////////////////////////////////////////////////////////
        // Pattern search algorithm written by Druttis.
        //
        // Patterns string is in the form of
        //
        //	0xMMVV, 0xMMVV, 0xMMVV
        //
        //	Where MM = Mask & VV = Value
        //
        //	Pattern Equals is doing the following match
        //
        //	(BB[p] & MM[p]) == VV[p]
        //
        //	Where BB = buffer data
        //
        //	That means :
        //
        //	a0, b0, c0, d0, e0 is equal to
        //
        //	1)	0xffa0, 0xffb0, 0x0000, 0x0000, 0xffe0
        //	2)	0x0000, 0x0000, 0x0000, 0x0000, 0x0000
        //	3)	0x8080, 0x3030, 0x0000, 0xffdd, 0xffee
        //
        //	I think you got the idea of it...BOOL _fastcall PatternEquals(LPBYTE buf, LPWORD pat, DWORD plen)
        //////////////////////////////////////////////////////////////////////
        private bool PatternEquals(byte[] buf, UInt32 pos, ushort[] pat, UInt32 plen)
        {
            //
            //	Just a counter
            UInt32 i;
            //
            //	Offset
            UInt32 ofs = 0;
            //
            //	Loop
            for (i = 0; plen > 0; i++)
            {
                //
                //	Compare mask buf and compare result
                //  <thohell>Swapped mask/data. Old code was buggy.</thohell>
                if ((buf[ofs + pos] & ((pat[ofs] & 0xff00) >> 8)) != (pat[ofs] & 0xff))
                    return false;
                // 
                //	Move ofs in zigzag direction
                plen--;
                if ((i & 1) == 0)
                    ofs += plen;
                else
                    ofs -= plen;
            }
            //
            //	Yep, we found
            return true;
        }

        //
        //	Search for the pattern, returns the pointer to buf+ofset matching
        //	the pattern or null.
        private UInt32 PatternSearch(byte[] buf, UInt32 blen, ushort[] pat, UInt32 plen)
        {
            //
            //	Offset and End of search
            UInt32 ofs;
            UInt32 end;
            //
            //	Buffer length and Pattern length may not be 0
            if ((blen == 0) || (plen == 0))
                return 0;
            //
            //	Calculate End of search
            end = blen - plen;
            //
            //	Do the booring loop
            for (ofs = 0; ofs < end; ofs++)
            {
                //
                //	Return offset to first byte of buf matching width the pattern
                if (PatternEquals(buf, ofs, pat, plen))
                    return ofs;
            }
            //
            //	Me no find, me return 0, NULL, nil
            return 0;
        }

        //////////////////////////////////////////////////////////////////////
        // MakeSearchPattern
        // -------------------------------------------------------------------
        // Convert a pattern-string into a pattern array for use with pattern 
        // search.
        //
        // <thohell>
        //////////////////////////////////////////////////////////////////////
        private void MakeSearchPattern(string pString, ushort wLen, ref ushort[] pat)
        {
            char[] tmp = pString.ToCharArray();

            for (int i = (tmp.Length / 2) - 1; tmp.Length > 0; i--)
            {
                byte value;
                string tempStr = new string(tmp);
                if (!Byte.TryParse(tempStr.Substring(i * 2), NumberStyles.HexNumber, null, out value))
                    pat[i] = 0;
                else
                    pat[i] = MakeWord(value, 0xff);

                Array.Resize(ref tmp, i * 2);
            }
        }

        /// <summary>
        /// Makes a 16 bit short from two bytes
        /// </summary>
        /// <param name="pValueLow">The low order value.</param>
        /// <param name="pValueHigh">The high order value.</param>
        /// <returns></returns>
        private ushort MakeWord(byte pValueLow, byte pValueHigh)
        {
            // ((WORD)(((BYTE)(((DWORD_PTR)(a)) & 0xff)) | ((WORD)((BYTE)(((DWORD_PTR)(b)) & 0xff))) << 8))
            if (pValueHigh == 0)
            {
                return (ushort)pValueLow;
            }
            int lTemp = pValueHigh << 8;
            return (ushort)(pValueLow | lTemp);
        }
    }
}
