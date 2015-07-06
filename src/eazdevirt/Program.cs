﻿using System;
using System.Linq;
using CommandLine;
using dnlib.DotNet;
using System.IO;
using dnlib.DotNet.Emit;
using System.Collections.Generic;

namespace eazdevirt
{
	public class Program
	{
		static void Main(String[] args)
		{
			var result = CommandLine.Parser.Default.ParseArguments
				<FindMethodsSubOptions,
				 GetKeySubOptions,
				 InstructionsSubOptions,
				 PositionSubOptions>(args);

			if (!result.Errors.Any())
			{
				if (result.Value is FindMethodsSubOptions)
				{
					DoFindMethods((FindMethodsSubOptions)result.Value);
				}
				else if (result.Value is GetKeySubOptions)
				{
					DoGetKey((GetKeySubOptions)result.Value);
				}
				else if (result.Value is InstructionsSubOptions)
				{
					DoInstructions((InstructionsSubOptions)result.Value);
				}
				else if (result.Value is PositionSubOptions)
				{
					DoPosition((PositionSubOptions)result.Value);
				}
			}
		}

		/// <summary>
		/// Perform "find-methods" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoFindMethods(FindMethodsSubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			EazVirtualizedMethod[] methods = module.FindVirtualizedMethods();

			if (methods.Length > 0) Console.WriteLine("Virtualized methods found: {0}", methods.Length);
			else Console.WriteLine("No virtualized methods found");

			foreach(var method in methods)
			{
				Console.WriteLine(method.Method.FullName);
				Console.WriteLine("  Position string: {0}", method.PositionString);
				Console.WriteLine("  Resource: {0}", method.ResourceStringId);
				Console.WriteLine("  Crypto key: {0}", method.ResourceCryptoKey);
			}
		}

		/// <summary>
		/// Perform "get-key" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoGetKey(GetKeySubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			EazVirtualizedMethod method = module.FindFirstVirtualizedMethod();
			Console.WriteLine("Key: {0}", method.ResourceCryptoKey);
		}

		/// <summary>
		/// Perform "instructions" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoInstructions(InstructionsSubOptions options)
		{
			EazModule module;
			if (!TryLoadModule(options.AssemblyPath, out module))
				return;

			EazVirtualizedMethod method = module.FindFirstVirtualizedMethod();
			if(method == null)
			{
				Console.WriteLine("No methods in assembly seem to be virtualized");
				return;
			}

			// The virtual-call-method should belong to the main virtualization type
			TypeDef virtualizationType = method.VirtualCallMethod.DeclaringType;
			var vInstructions = EazVirtualInstruction.FindAllInstructions(module, virtualizationType);

			if (vInstructions.Length > 0)
			{
				// Get # of identified instructions
				Int32 identified = 0;
				foreach (var v in vInstructions)
					if (v.IsIdentified) identified++;

				// Get % of identified instructions as a string
				String percentIdentified;
				if (identified == 0)
					percentIdentified = "0%";
				else if (identified == vInstructions.Length)
					percentIdentified = "100%";
				else
					percentIdentified = Math.Floor(
						(((double)identified) / ((double)vInstructions.Length)) * 100d
					) + "%";

				Console.WriteLine("Virtual instruction types found: {0}", vInstructions.Length);
				Console.WriteLine("{0}/{1} instruction types identified ({2})",
					identified, vInstructions.Length, percentIdentified);

				foreach (var v in vInstructions)
				{
					if(v.IsIdentified)
						Console.WriteLine("Instruction: {0} (virtual opcode = {1})", v.OpCode, v.VirtualOpCode);
					else if(!options.OnlyIdentified)
						Console.WriteLine("Instruction: {0}", v.VirtualOpCode);

					if (v.IsIdentified || !options.OnlyIdentified)
					{
						Console.WriteLine("  Operand type: {0}", v.OperandType);
						Console.WriteLine("  Delegate method: {0}", v.DelegateMethod.FullName);
					}
				}

				// Print operand information
				if(options.Operands)
				{
					var operandTypeDict = new Dictionary<Int32, Int32>();
					foreach (var vInstr in vInstructions)
					{
						var type = vInstr.OperandType;
						if (operandTypeDict.ContainsKey(type))
							operandTypeDict[type] = (operandTypeDict[type] + 1);
						else operandTypeDict.Add(type, 1);
					}

					Console.WriteLine();
					Console.WriteLine("Virtual operand type counts:");
					foreach(var kvp in operandTypeDict)
						Console.WriteLine("  Operand {0}: {1} occurrence(s)", kvp.Key, kvp.Value);
				}
			}
			else Console.WriteLine("No virtual instructions found?");
		}

		/// <summary>
		/// Perform "position" verb.
		/// </summary>
		/// <param name="options">Options</param>
		static void DoPosition(PositionSubOptions options)
		{
			Int64 position = 0;

			if (options.Key.HasValue)
			{
				// This doesn't work yet: Command line parser can't parse Nullable?
				position = EazPosition.FromString(options.PositionString, options.Key.Value);
			}
			else if (options.AssemblyPath != null)
			{
				EazModule module;
				if (!TryLoadModule(options.AssemblyPath, out module))
					return;

				EazVirtualizedMethod method = module.FindFirstVirtualizedMethod();
				if (method != null)
				{
					try
					{
						position = EazPosition.FromString(options.PositionString, method.ResourceCryptoKey);
					}
					catch (FormatException e)
					{
						Console.WriteLine(e.Message);
						return;
					}
				}
				else
				{
					Console.WriteLine("No virtualized methods found in specified assembly");
					return;
				}
			}
			else
			{
				Console.WriteLine("Provide either the crypto key or assembly from which to extract the crypto key");
				return;
			}

			Console.WriteLine("{0} => {1:X8}", options.PositionString, position);
		}

		static Boolean TryLoadModule(String path, out EazModule module)
		{
			try
			{
				module = new EazModule(path);
			}
			catch (IOException e)
			{
				Console.WriteLine(e.Message);
				module = null;
				return false;
			}

			return true;
		}
	}
}