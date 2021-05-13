using System;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace NotITG.External
{

	// Use this one
	public class NotITGHandler
	{
		public NotITGExternalApi.NOTITG_VERSION Version;
		public Process Process;
		public ProcessMemoryMin.MemoryReader MemoryReader;
		public int VersionDate;
		public IntPtr ExternalBaseAddress;
		private bool IsFilenameKnown = false;

		public bool HasNotITG = false;
		public event EventHandler OnExit;

		public int IndexLimit;

		private void Reset()
		{
			if (HasNotITG)
				OnExit?.Invoke(this, EventArgs.Empty);
			this.Version = NotITGExternalApi.NOTITG_VERSION.UNKNOWN;
			this.Process = null;
			if (this.MemoryReader != null) this.MemoryReader.Close();
			this.MemoryReader = null;
			this.HasNotITG = false;
			this.VersionDate = -1;
			this.IndexLimit = -1;
		}
		public bool TryScan()
		{
			foreach (Process proc in Process.GetProcesses())
			{
				if (this.IsFilenameKnown)
				{
					foreach (NotITGExternalApi.NOTITG_VERSION enum_ver in Enum.GetValues(typeof(NotITGExternalApi.NOTITG_VERSION)))
					{
						if (enum_ver == NotITGExternalApi.NOTITG_VERSION.UNKNOWN) continue;
						var details = new NotITGExternalApi.ExternalDetails(enum_ver);
						try
						{
							if (proc.MainModule.FileName.Split('\\').LastOrDefault().ToLower().Equals(details.Default_FileName.ToLower()))
							{
								// Got!
								proc.EnableRaisingEvents = true;
								proc.Exited += (s, e) => { Reset(); };
								this.Version = enum_ver;
								this.Process = proc;
								this.HasNotITG = true;
								this.MemoryReader = new ProcessMemoryMin.MemoryReader(proc);
								this.VersionDate = details.VersionDate;
								this.ExternalBaseAddress = details.ExternalAddress;
								this.IndexLimit = details.IndexLimit;
								return true;
							}
						}
						catch { }
					}
				}
				else
				{
					var memory = new ProcessMemoryMin.MemoryReader(proc);

					try
					{
						foreach (NotITGExternalApi.NOTITG_VERSION enum_ver in Enum.GetValues(typeof(NotITGExternalApi.NOTITG_VERSION)))
						{
							if (enum_ver == NotITGExternalApi.NOTITG_VERSION.UNKNOWN) continue;
							var details = new NotITGExternalApi.ExternalDetails(enum_ver);
							byte[] version_byte = Encoding.ASCII.GetBytes(details.VersionDate.ToString());

							string read_date = Encoding.ASCII.GetString(memory.Read(details.BuildAddress, (uint)version_byte.Length));
							if (read_date.ToLower().Equals(details.VersionDate.ToString().ToLower()))
							{
								// Got!
								proc.EnableRaisingEvents = true;
								proc.Exited += (s, e) => { Reset(); };
								this.Version = enum_ver;
								this.Process = proc;
								this.HasNotITG = true;
								this.MemoryReader = memory;
								this.VersionDate = details.VersionDate;
								this.ExternalBaseAddress = details.ExternalAddress;
								this.IndexLimit = details.IndexLimit;
								return true;
							}
						}

						memory.Close();
					}
					catch { }
				}
			}
			return false;
		}

		public int GetExternal(int index)
		{
			if (!HasNotITG) return 0;
			if (!(index >= 0 && index <= this.IndexLimit))
				throw new Exception("Index range out of bounds.");

			byte offset = (byte)(index * 4);
			byte[] a = this.MemoryReader.Read((IntPtr)this.ExternalBaseAddress + offset, sizeof(int));
			return BitConverter.ToInt32(a, 0);
		}
		public void SetExternal(int index, int flag)
		{
			if (!HasNotITG) return;
			if (!(index >= 0 && index <= this.IndexLimit))
				throw new Exception("Index range out of bounds.");

			byte[] b = BitConverter.GetBytes(flag);
			byte offset = (byte)(index * 4);
			this.MemoryReader.Write((IntPtr)this.ExternalBaseAddress + offset, b);
		}

		public NotITGHandler(bool isFilenameKnown = true)
		{
			Reset();
			this.IsFilenameKnown = isFilenameKnown;
		}
	}

	// Minified Process Memory Reader
	public class ProcessMemoryMin
	{
		[DllImport("kernel32.dll")]
		public static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, Int32 bInheritHandle, UInt32 dwProcessId);
		[DllImport("kernel32.dll")]
		public static extern Int32 CloseHandle(IntPtr hObject);
		[DllImport("kernel32.dll")]
		public static extern Int32 ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] byte[] buffer, UInt32 size, out IntPtr lpNumberOfBytesRead);
		[DllImport("kernel32.dll")]
		public static extern Int32 WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In, Out] byte[] buffer, UInt32 size, out IntPtr lpNumberOfBytesWritten);

		public enum ProcessVM
		{
			READ = 0x10,
			WRITE = 0x20,
			OPERATION = 0x8
		}

		public class MemoryReader
		{
			public IntPtr m_hProcess = IntPtr.Zero;
			public MemoryReader(Process proc)
			{
				if (m_hProcess != IntPtr.Zero) return;
				m_hProcess =
					OpenProcess((uint)(ProcessVM.READ | ProcessVM.WRITE | ProcessVM.OPERATION), 1, (uint)proc.Id);
			}
			public void Close()
			{
				if (m_hProcess == IntPtr.Zero) return;
				if (CloseHandle(m_hProcess) == 0)
					throw new Exception("Closehandle Failed");
			}

			/*public byte[] Read(IntPtr address, uint bytes_to_read, out int bytes_read)
			{
				byte[] buffer = new byte[bytes_to_read];
				ReadProcessMemory(m_hProcess, address, buffer, bytes_to_read, out IntPtr ptrBytesRead);
				bytes_read = ptrBytesRead.ToInt32();
				return buffer;
			}
			public void Write(IntPtr address, byte[] bytes_to_write, out int bytes_written)
			{
				WriteProcessMemory(m_hProcess, address, bytes_to_write, (uint)bytes_to_write.Length, out IntPtr ptrBytesWrite);
				bytes_written = ptrBytesWrite.ToInt32();
			}*/
			public byte[] Read(IntPtr address, uint bytes_to_read)
			{
				byte[] buffer = new byte[bytes_to_read];
				ReadProcessMemory(m_hProcess, address, buffer, bytes_to_read, out IntPtr _);
				return buffer;
			}
			public void Write(IntPtr address, byte[] bytes_to_write)
			{
				// Yeah we dont need the bytes_written part. We can just discard that.
				WriteProcessMemory(m_hProcess, address, bytes_to_write, (uint)bytes_to_write.Length, out IntPtr _);
			}
		}

	}

	public class NotITGExternalApi
	{
		public enum NOTITG_VERSION
		{
			UNKNOWN,
			V1,
			V2,
			V3,
			V3_1,
			V4,
			V4_0_1,
			V4_2,
		}

		public class ExternalDetails
		{
			public string Default_FileName { get; set; }
			public int VersionDate { get; set; }
			public IntPtr BuildAddress { get; set; }
			public IntPtr ExternalAddress { get; set; }
			public int IndexLimit { get; set; }

			public ExternalDetails(NOTITG_VERSION version)
			{
				switch (version)
				{
					case NOTITG_VERSION.V1:
						this.Default_FileName = "NotITG.exe";
						this.VersionDate = 20161224;
						this.BuildAddress = (IntPtr)0x006AED20;
						this.ExternalAddress = (IntPtr)0x00896950;
						this.IndexLimit = 9;
						break;
					case NOTITG_VERSION.V2:
						this.Default_FileName = "NotITG-170405.exe";
						this.VersionDate = 20170405;
						this.BuildAddress = (IntPtr)0x006B7D40;
						this.ExternalAddress = (IntPtr)0x008A0880;
						this.IndexLimit = 9;
						break;
					case NOTITG_VERSION.V3:
						this.Default_FileName = "NotITG-V3.exe";
						this.VersionDate = 20180617;
						this.BuildAddress = (IntPtr)0x006DFD60;
						this.ExternalAddress = (IntPtr)0x008CC9D8;
						this.IndexLimit = 63;
						break;
					case NOTITG_VERSION.V3_1:
						this.Default_FileName = "NotITG-V3.1.exe";
						this.VersionDate = 20180827;
						this.BuildAddress = (IntPtr)0x006E7D60;
						this.ExternalAddress = (IntPtr)0x008BE0F8;
						this.IndexLimit = 63;
						break;
					case NOTITG_VERSION.V4:
						this.Default_FileName = "NotITG-V4.exe";
						this.VersionDate = 20200112;
						this.BuildAddress = (IntPtr)0x006E0E60;
						this.ExternalAddress = (IntPtr)0x008BA388;
						this.IndexLimit = 63;
						break;
					case NOTITG_VERSION.V4_0_1:
						this.Default_FileName = "NotITG-V4.0.1.exe";
						this.VersionDate = 20200126;
						this.BuildAddress = (IntPtr)0x006C5E40;
						this.ExternalAddress = (IntPtr)0x00897D10;
						this.IndexLimit = 63;
						break;
					case NOTITG_VERSION.V4_2:
						this.Default_FileName = "NotITG-V4.2.0.exe";
						this.VersionDate = 20210420;
						this.BuildAddress = (IntPtr)0x006FAD40;
						this.ExternalAddress = (IntPtr)0x008BFF38;
						this.IndexLimit = 255;
						break;
					default:
						throw new Exception("Version unknown!");
				}
			}
		}
	}
}