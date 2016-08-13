﻿using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Utilities;
using Microsoft.Diagnostics.Runtime.Utilities.Pdb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.RuntimeExt
{
    // This is taken from the Samples\FileAndLineNumbers projects from microsoft/clrmd,
    // and replaces the previously-available SourceLocation functionality.

    public class SourceLocation
    {
        public string FilePath;
        public int LineNumber;
        public int LineNumberEnd;
        public int ColStart;
        public int ColEnd;
    }

    public static class ClrFrameExtensions
    {
        static Dictionary<PdbInfo, PdbReader> s_pdbReaders = new Dictionary<PdbInfo, PdbReader>();
        public static SourceLocation GetSourceLocation(this ClrStackFrame frame)
        {
            PdbReader reader = GetReaderForFrame(frame);
            if (reader == null)
                return null;

            PdbFunction function = reader.GetFunctionFromToken(frame.Method.MetadataToken);
            int ilOffset = FindIlOffset(frame);

            return FindNearestLine(function, ilOffset);
        }

        private static SourceLocation FindNearestLine(PdbFunction function, int ilOffset)
        {
            int distance = int.MaxValue;
            SourceLocation nearest = null;

            foreach (PdbSequencePointCollection sequenceCollection in function.SequencePoints)
            {
                foreach (PdbSequencePoint point in sequenceCollection.Lines)
                {
                    int dist = (int)Math.Abs(point.Offset - ilOffset);
                    if (dist < distance)
                    {
                        if (nearest == null)
                            nearest = new SourceLocation();

                        nearest.FilePath = sequenceCollection.File.Name;
                        nearest.LineNumber = (int)point.LineBegin;
                        nearest.LineNumberEnd = (int)point.LineEnd;
                        nearest.ColStart = (int)point.ColBegin;
                        nearest.ColEnd = (int)point.ColEnd;
                    }
                }
            }

            return nearest;
        }

        private static int FindIlOffset(ClrStackFrame frame)
        {
            ulong ip = frame.InstructionPointer;
            int last = -1;
            foreach (ILToNativeMap item in frame.Method.ILOffsetMap)
            {
                if (item.StartAddress > ip)
                    return last;

                if (ip <= item.EndAddress)
                    return item.ILOffset;

                last = item.ILOffset;
            }

            return last;
        }


        private static PdbReader GetReaderForFrame(ClrStackFrame frame)
        {
            ClrModule module = frame.Method?.Type?.Module;
            PdbInfo info = module?.Pdb;

            PdbReader reader = null;
            if (info != null)
            {
                if (!s_pdbReaders.TryGetValue(info, out reader))
                {
                    SymbolLocator locator = GetSymbolLocator(module);
                    string pdbPath = locator.FindPdb(info);
                    if (pdbPath != null)
                        reader = new PdbReader(pdbPath);

                    s_pdbReaders[info] = reader;
                }
            }

            return reader;
        }

        private static SymbolLocator GetSymbolLocator(ClrModule module)
        {
            return module.Runtime.DataTarget.SymbolLocator;
        }
    }
}
