/*
   
    openDICOM.NET openDICOM# 0.2

    openDICOM# provides a library for DICOM related development on Mono.
    Copyright (C) 2006-2007  Albert Gnandt

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA 02110-1301 USA


    $Id$
*/
using System;
using openDicom.DataStructure;
using openDicom.Registry;


namespace openDicom.Encoding
{

    /// <summary>
    ///     This class represents the specific DICOM VR Short Text (ST).
    /// </summary>    
    public sealed class ShortText: ValueRepresentation
    {
        public ShortText(Tag tag): base("ST", tag) {}
        
        public override string ToLongString()
        {
            return "Short Text (ST)";
        }

        protected override Array DecodeImproper(byte[] bytes)
        {
            string shortText = TransferSyntax.ToString(bytes);
            shortText = shortText.TrimEnd(null);
            return new string[] { shortText };
        }
        
        protected override Array DecodeProper(byte[] bytes)
        {
            string shortText = TransferSyntax.ToString(bytes);
            ValueMultiplicity vm = Tag.GetDictionaryEntry().VM;
            if (vm.Equals(1) || vm.IsUndefined)
            {
                if (shortText.Length <= 1024)
                    shortText = shortText.TrimEnd(null);
                else
                    throw new EncodingException(
                        "A value of max. 1024 characters is only allowed.",
                        Tag, Name + "/shortText", shortText);
            }
            else
                throw new EncodingException(
                    "Multiple values are not allowed within this field.", Tag,
                    Name + "/VM, " + Name + "/shortText", 
                    vm.ToString() + ", " + shortText);
            return new string[] { shortText };
        }
    }

}
