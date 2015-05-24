using Newtonsoft.Json;
using System;
using System.Collections.Generic;

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

        private List<int> _softdeviceReq;
        [JsonProperty(PropertyName = "softdevice_req")]
        public List<int> SoftdeviceReq
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