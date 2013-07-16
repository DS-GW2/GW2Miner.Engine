using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace GW2SessionKey
{
    #region ProcessMemoryReader class

    //Thanks goes to Arik Poznanski for P/Invokes and methods needed to read and write the Memory
    //For more information refer to "Minesweeper, Behind the scenes" article by Arik Poznanski at Codeproject.com
    class ProcessMemoryReader
    {
        [Flags]
        public enum memState
        {
            MEM_COMMIT  = (0x1000),
            MEM_DECOMMIT = (0x4000),      
            MEM_RELEASE = (0x8000),      
            MEM_FREE = (0x10000)
        }

        [Flags]
        public enum memType
        {
            MEM_PRIVATE = (0x20000),
            MEM_MAPPED = (0x40000),
            MEM_RESET = (0x80000),
            MEM_IMAGE = (0x1000000)
        }

        // typedef struct _MEMORY_BASIC_INFORMATION {
        //   PVOID  BaseAddress;
        //   PVOID  AllocationBase;
        //   DWORD  AllocationProtect;
        //   SIZE_T RegionSize;
        //   DWORD  State;
        //   DWORD  Protect;
        //   DWORD  Type;
        // } MEMORY_BASIC_INFORMATION, *PMEMORY_BASIC_INFORMATION;
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public uint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        };

        public ProcessMemoryReader()
        {
        }

        /// <summary>	
        /// Process from which to read		
        /// </summary>
        public Process ReadProcess
        {
            get
            {
                return m_ReadProcess;
            }
            set
            {
                m_ReadProcess = value;
            }
        }

        private Process m_ReadProcess = null;

        private IntPtr m_hProcess = IntPtr.Zero;

        public void OpenProcess()
        {
            ProcessMemoryReaderApi.ProcessAccessType access;
            access = ProcessMemoryReaderApi.ProcessAccessType.PROCESS_VM_READ
                | ProcessMemoryReaderApi.ProcessAccessType.PROCESS_QUERY_INFORMATION;
            m_hProcess = ProcessMemoryReaderApi.OpenProcess((uint)access, 0, (uint)m_ReadProcess.Id);
        }

        public void CloseHandle()
        {
            //try
            //{
                int iRetValue;
                iRetValue = ProcessMemoryReaderApi.CloseHandle(m_hProcess);
                if (iRetValue == 0)
                {
                    throw new Exception("CloseHandle failed");
                }
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("Error: {0}", ex.Message);
            //}
        }

        public MEMORY_BASIC_INFORMATION VirtualQueryEx(IntPtr lpAddress, out uint bytesRead)
        {
            MEMORY_BASIC_INFORMATION buffer = new MEMORY_BASIC_INFORMATION();
            bytesRead = 0;

            // Initialize unmanged memory to hold the struct.
            //IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(buffer));

            //try
            //{
                bytesRead = ProcessMemoryReaderApi.VirtualQueryEx(m_hProcess, lpAddress, out buffer, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));
                //buffer = (MEMORY_BASIC_INFORMATION)Marshal.PtrToStructure(pnt, typeof(MEMORY_BASIC_INFORMATION));
                if (bytesRead == 0)
                {
                    //throw new Exception("VirtualQueryEx failed");
                    //Console.WriteLine(Marshal.GetLastWin32Error()); 
                }
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("Error: {0}", ex.Message);
            //}

            return buffer;
        }

        public byte[] ReadProcessMemory(IntPtr MemoryAddress, uint bytesToRead, out int bytesRead)
        {
            byte[] buffer = new byte[bytesToRead];

            IntPtr ptrBytesRead;
            ProcessMemoryReaderApi.ReadProcessMemory(m_hProcess, MemoryAddress, buffer, bytesToRead, out ptrBytesRead);

            bytesRead = ptrBytesRead.ToInt32();

            return buffer;
        }

        public void WriteProcessMemory(IntPtr MemoryAddress, byte[] bytesToWrite, out int bytesWritten)
        {
            IntPtr ptrBytesWritten;
            ProcessMemoryReaderApi.WriteProcessMemory(m_hProcess, MemoryAddress, bytesToWrite, (uint)bytesToWrite.Length, out ptrBytesWritten);

            bytesWritten = ptrBytesWritten.ToInt32();
        }

        /// <summary>
        /// ProcessMemoryReader is a class that enables direct reading a process memory
        /// </summary>
        class ProcessMemoryReaderApi
        {
            // constants information can be found in <winnt.h>
            [Flags]
            public enum ProcessAccessType
            {
                PROCESS_TERMINATE = (0x0001),
                PROCESS_CREATE_THREAD = (0x0002),
                PROCESS_SET_SESSIONID = (0x0004),
                PROCESS_VM_OPERATION = (0x0008),
                PROCESS_VM_READ = (0x0010),
                PROCESS_VM_WRITE = (0x0020),
                PROCESS_DUP_HANDLE = (0x0040),
                PROCESS_CREATE_PROCESS = (0x0080),
                PROCESS_SET_QUOTA = (0x0100),
                PROCESS_SET_INFORMATION = (0x0200),
                PROCESS_QUERY_INFORMATION = (0x0400)
            };

            // function declarations are found in the MSDN and in <winbase.h> 

            // SIZE_T WINAPI VirtualQueryEx(
            //   _In_      HANDLE hProcess,
            //   _In_opt_  LPCVOID lpAddress,
            //   _Out_     PMEMORY_BASIC_INFORMATION lpBuffer,
            //   _In_      SIZE_T dwLength
            // );
            [DllImport("kernel32.dll")]
            public static extern uint VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

            //		HANDLE OpenProcess(
            //			DWORD dwDesiredAccess,  // access flag
            //			BOOL bInheritHandle,    // handle inheritance option
            //			DWORD dwProcessId       // process identifier
            //			);
            [DllImport("kernel32.dll")]
            public static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, Int32 bInheritHandle, UInt32 dwProcessId);

            //		BOOL CloseHandle(
            //			HANDLE hObject   // handle to object
            //			);
            [DllImport("kernel32.dll")]
            public static extern Int32 CloseHandle(IntPtr hObject);

            //		BOOL ReadProcessMemory(
            //			HANDLE hProcess,              // handle to the process
            //			LPCVOID lpBaseAddress,        // base of memory area
            //			LPVOID lpBuffer,              // data buffer
            //			SIZE_T nSize,                 // number of bytes to read
            //			SIZE_T * lpNumberOfBytesRead  // number of bytes read
            //			);
            [DllImport("kernel32.dll")]
            public static extern Int32 ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] byte[] buffer, UInt32 size, out IntPtr lpNumberOfBytesRead);

            //		BOOL WriteProcessMemory(
            //			HANDLE hProcess,                // handle to process
            //			LPVOID lpBaseAddress,           // base of memory area
            //			LPCVOID lpBuffer,               // data buffer
            //			SIZE_T nSize,                   // count of bytes to write
            //			SIZE_T * lpNumberOfBytesWritten // count of bytes written
            //			);
            [DllImport("kernel32.dll")]
            public static extern Int32 WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] byte[] buffer, UInt32 size, out IntPtr lpNumberOfBytesWritten);
        }
    }
    #endregion
}
