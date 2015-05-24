/*************************************************************************************************************************************************
 * Copyright (c) 2015, Nordic Semiconductor
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the
 * documentation and/or other materials provided with the distribution.
 *
 * 3. Neither the name of the copyright holder nor the names of its contributors may be used to endorse or promote products derived from this
 * software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
 * ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE
 * USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 ************************************************************************************************************************************************/
using System;

namespace No.NordicSemi.Droid.DFU.Exception
{
    public class UnknownResponseException : Java.Lang.Throwable
    {
        private static readonly long _serialVersionUID = -8716125467309979289L;
	    private static readonly char[] _hexArray = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

	    private readonly byte[] _response;
	    private readonly int _expectedOpCode;

        public override String Message
        {
            get
            {
                return String.Format("%s (response: %s, expected: 0x10%02X..)", base.Message, BytesToHex(_response, 0, _response.Length), _expectedOpCode);
            }
        }
        
        public UnknownResponseException(String message, byte[] response, int expectedOpCode) 
            : base(message)
        {
		    _response = response != null ? response : new byte[0];
		    _expectedOpCode = expectedOpCode;
	    }

	    public static String BytesToHex(byte[] bytes, int start, int length) 
        {
		    if (bytes == null || bytes.Length <= start || length <= 0)
			    return "";

		    var maxLength = Math.Min(length, bytes.Length - start);
		    var hexChars = new char[maxLength * 2];
		    for (var j = 0; j < maxLength; j++) 
            {
			    var v = bytes[start + j] & 0xFF;
			    hexChars[j * 2] = _hexArray[(int)((uint)v >> 4)];
			    hexChars[j * 2 + 1] = _hexArray[v & 0x0F];
		    }
		    return "0x" + new String(hexChars);
	    }
    }
}