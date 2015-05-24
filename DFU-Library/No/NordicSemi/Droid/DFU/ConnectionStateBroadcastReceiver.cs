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
using Android.Bluetooth;
using Android.Content;
using Android.Util;
using System;

namespace No.NordicSemi.Droid.DFU
{
    [BroadcastReceiver]
    public class ConnectionStateBroadcastReceiver : BroadcastReceiver
    {
        private readonly string _tag = "ConnectionStateBroadcastReceiver";
        private string _deviceAddress;
        private int _connectionState;

        public ConnectionStateBroadcastReceiver(ref string deviceAddress, ref int connectionState)
        {
            _deviceAddress = deviceAddress;
            _connectionState = connectionState;
        }

        public override void OnReceive(Context context, Intent intent) 
        {
            // Obtain the device and check it this is the one that we are connected to
			var device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
			if (!device.Address.Equals(_deviceAddress))
				return;

			var action = intent.Action;
            Log.Info(_tag, "Action received: " + action);
			_connectionState = DfuBaseService.StateDisconnected;
        }
    }
}