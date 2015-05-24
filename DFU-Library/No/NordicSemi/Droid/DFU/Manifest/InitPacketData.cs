/*
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
 */
using Newtonsoft.Json;
using System;

namespace No.NordicSemi.Droid.DFU.Manifest
{
    public class InitPacketData
    {
        private int _packetVersion;
        [JsonProperty(PropertyName = "packet_version")]
	    public int PacketVersion
        {
            get
            {
    		    return _packetVersion;
            }
            protected set
            {
                _packetVersion = value;
            }
	    }

        private int _compressionType;
        [JsonProperty(PropertyName ="compression_type")]
        public int CompressionType 
        {
            get
            {
                return _compressionType;
            }
            protected set
            {
                _compressionType = value;
            }
	    }

        private long _applicationVersion;
        [JsonProperty(PropertyName = "application_version")]
        public long ApplicationVersion
        {
            get
            {
                return _applicationVersion;
            }
            protected set
            {
                _applicationVersion = value;
            }
	    }

        private int _deviceRevision;
        [JsonProperty(PropertyName = "device_revision")]
        public int DeviceRevision
        {
            get
            {
                return _deviceRevision;
            }
            protected set 
            {
                _deviceRevision = value;
            }
	    }

        private int _deviceType;
        [JsonProperty(PropertyName = "device_type")]
        public int DeviceType
        {
            get
            {
                return _deviceType;
            }
            protected set
            {
                _deviceType = value;
            }
	    }

        private int _firmwareCRC16;
        [JsonProperty(PropertyName = "firmware_crc16")]
        public int FirmwareCRC16
        {
            get 
            {
                return _firmwareCRC16;
            }
            protected set 
            {
                _firmwareCRC16 = value;
            }
	    }

        private String _firmwareHash;
        [JsonProperty(PropertyName = "firmware_hash")]
        public String FirmwareHash
        {
            get
            {
                return _firmwareHash;
            }
            protected set
            {
                _firmwareHash = value;
            }
	    }

        private System.Collections.Generic.List<int> _softdeviceReq;
        [JsonProperty(PropertyName = "softdevice_req")]
        public System.Collections.Generic.List<int> SoftdeviceReq
        {
            get
            {
                return _softdeviceReq;
            }
            protected set
            {
                _softdeviceReq = value;
            }
	    }
    }
}