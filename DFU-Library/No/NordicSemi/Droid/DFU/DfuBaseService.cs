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
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Util;
using Android.Bluetooth;
using Java.IO;
using Android.Util;
using No.NordicSemi.Droid.DFU.Events;
using Java.Lang;

using String = System.String;
using Android.Preferences;
using Android.Support.V4.Content;
using System.Threading.Tasks;
using No.NordicSemi.Droid.Error;
using No.NordicSemi.Droid.DFU.Exception;
using Java.Lang.Reflect;

namespace No.NordicSemi.Droid.DFU
{
    /**
     * The DFU Service provides full support for Over-the-Air (OTA) Device Firmware Update (DFU) by Nordic Semiconductor.
     * With the Soft Device 7.0.0+ it allows to upload a new Soft Device, new Bootloader and a new Application. For older soft devices only the Application update is supported.
     * <p>
     * To run the service to your application extend it in your project and overwrite the missing method. Remember to add your class to the AndroidManifest.xml file.
     * </p>
     * <p>
     * Start the service with the following parameters:
     * <p/>
     * <pre>
     * readonly Intent service = new Intent(this, YourDfuService.class);
     * service.putExtra(DfuService.ExtraDeviceAddress, mSelectedDevice.getAddress()); // Target device address
     * service.putExtra(DfuService.ExtraDeviceName, mSelectedDevice.getName()); // This name will be shown on the notification
     * service.putExtra(DfuService.ExtraFileMimeType, _fileType == DfuService.TypeAuto ? YourDfuService.MimeTypeZip : YourDfuService.MimeTypeOctetStream);
     * service.putExtra(DfuService.ExtraFileType, _fileType);
     * service.putExtra(DfuService.ExtraFilePath, mFilePath);
     * service.putExtra(DfuService.ExtraFileUri, mFileStreamUri);
     * // optionally
     * service.putExtra(DfuService.ExtraInitFilePath, mInitFilePath);
     * service.putExtra(DfuService.ExtraInitFileUri, mInitFileStreamUri);
     * service.putExtra(DfuService.ExtraRestoreBond, mRestoreBond);
     * StartService(service);
     * </pre>
     * <p/>
     * The {@link #ExtraFileMimeType} and {@link #ExtraFileType} parameters are optional. If not provided the application upload from HEX/BIN file is assumed.
     * The service API is compatible with previous versions.
     * </p>
     * <p>
     * The service will show its progress on the notification bar and will send local broadcasts to the application.
     * </p>
     */
    public abstract class DfuBaseService : IntentService
    {
        /**
	     * The address of the device to update.
	     */
	    public const string ExtraDeviceAddress = "No.NordicSemi.Doid.DFU.extra.ExtraDeviceAddress";
	    /**
	     * The optional device name. This name will be shown in the notification.
	     */
	    public const string ExtraDeviceName = "No.NordicSemi.Doid.DFU.extra.ExtraDeviceName";
	    /**
	     * <p>
	     * If the new firmware (application) does not share the bond information with the old one, the bond information is lost. Set this flag to <code>true</code>
	     * to make the service create new bond with the new application when the upload is done (and remove the old one). When set to <code>false</code> (default),
	     * the DFU service assumes that the LTK is shared between them. Note: currently it is not possible to remove the old bond without creating a new one so if
	     * your old application supported bonding while the new one does not you have to modify the source code yourself.
	     * </p>
	     * <p>
	     * In case of updating the soft device the application is always removed together with the bond information.
	     * </p>
	     * <p>
	     * Search for occurrences of ExtraRestoreBond in this file to check the implementation and get more details.
	     * </p>
	     */
	    public const string ExtraRestoreBond = "No.NordicSemi.Doid.DFU.extra.ExtraRestoreBond";
	    /**
	     * <p>This flag indicated whether the bond information should be kept or removed after an upgrade of the Application.
	     * If an application is being updated on a bonded device with the DFU Bootloader that has been configured to preserve the bond information for the new application,
	     * set it to <code>true</code>.</p>
	     *
	     * <p>By default the DFU Bootloader clears the whole application's memory. It may be however configured in the \Nordic\nrf51\components\libraries\bootloaderDfu\dfu_types.h
	     * file (line 56: <code>#define DFU_APP_DATA_RESERVED 0x0000</code>) to preserve some pages. The BLE_APP_HRMDfu sample app stores the LTK and System Attributes in the first
	     * two pages, so in order to preserve the bond information this value should be changed to 0x0800 or more.
	     * When those data are preserved, the new Application will notify the app with the Service Changed indication when launched for the first time. Otherwise this
	     * service will remove the bond information from the phone and force to refresh the device cache (see {@link #refreshDeviceCache(android.bluetooth.BluetoothGatt, Boolean)}).</p>
	     *
	     * <p>In contrast to {@link #ExtraRestoreBond} this flag will not remove the old bonding and recreate a new one, but will keep the bond information untouched.</p>
	     * <p>The default value of this flag is <code>false</code></p>
	     */
	    public const string ExtraKeepBond = "No.NordicSemi.Doid.DFU.extra.ExtraKeepBond";
	    /**
	     * A path to the file with the new firmware. It may point to a HEX, BIN or a ZIP file.
	     * Some file manager applications return the path as a String while other return a Uri. Use the {@link #ExtraFileUri} in the later case.
	     */
	    public const string ExtraFilePath = "No.NordicSemi.Doid.DFU.extra.ExtraFilePath";
	    /**
	     * See {@link #ExtraFilePath} for details.
	     */
	    public const string ExtraFileUri = "No.NordicSemi.Doid.DFU.extra.ExtraFileUri";
	    /**
	     * The Init packet URI. This file is required if the Extended Init Packet is required (SDK 7.0+). Must point to a 'dat' file corresponding with the selected firmware.
	     * The Init packet may contain just the CRC (in case of older versions of DFU) or the Extended Init Packet in binary format (SDK 7.0+).
	     */
	    public const string ExtraInitFilePath = "No.NordicSemi.Doid.DFU.extra.ExtraInitFilePath";
	    /**
	     * The Init packet URI. This file is required if the Extended Init Packet is required (SDK 7.0+). Must point to a 'dat' file corresponding with the selected firmware.
	     * The Init packet may contain just the CRC (in case of older versions of DFU) or the Extended Init Packet in binary format (SDK 7.0+).
	     */
	    public const string ExtraInitFileUri = "No.NordicSemi.Doid.DFU.extra.ExtraInitFileUri";
	    /**
	     * The input file mime-type. Currently only "application/zip" (ZIP) or "application/octet-stream" (HEX or BIN) are supported. If this parameter is
	     * empty the "application/octet-stream" is assumed.
	     */
	    public const string ExtraFileMimeType = "No.NordicSemi.Doid.DFU.extra.EXTRA_MIME_TYPE";
	    // Since the DFU Library version 0.5 both HEX and BIN files are supported. As both files have the same MIME TYPE the distinction is made based on the file extension.
	    public const string MimeTypeOctetStream = "application/octet-stream";
	    public const string MimeTypeZip = "application/zip";
	    /**
	     * This optional extra parameter may contain a file type. Currently supported are:
	     * <ul>
	     * <li>{@link #TypeSoftDevice} - only Soft Device update</li>
	     * <li>{@link #TypeBootloader} - only Bootloader update</li>
	     * <li>{@link #TypeApplication} - only application update</li>
	     * <li>{@link #TypeAuto} - the file is a ZIP file that may contain more than one HEX/BIN + DAT files. Since SDK 8.0 the ZIP Distribution packet is a recommended
	     * way of delivering firmware files. Please, see the DFU documentation for more details. A ZIP distribution packet may be created using the 'nrf utility'
	     * command line application, that is a part of Master Control Panel 3.8.0.The ZIP file MAY contain only the following files:
	     * <b>softdevice.hex/bin</b>, <b>bootloader.hex/bin</b>, <b>application.hex/bin</b> to determine the type based on its name. At lease one of them MUST be present.
	     * </li>
	     * </ul>
	     * If this parameter is not provided the type is assumed as follows:
	     * <ol>
	     * <li>If the {@link #ExtraFileMimeType} field is <code>null</code> or is equal to {@value #MimeTypeOctetStream} - the {@link #TypeApplication} is assumed.</li>
	     * <li>If the {@link #ExtraFileMimeType} field is equal to {@value #MimeTypeZip} - the {@link #TypeAuto} is assumed.</li>
	     * </ol>
	     */
	    public const string ExtraFileType = "No.NordicSemi.Doid.DFU.extra.ExtraFileType";
	    /**
	     * <p>
	     * The file contains a new version of Soft Device.
	     * </p>
	     * <p>
	     * Since DFU Library 7.0 all firmware may contain an Init packet. The Init packet is required if Extended Init Packet is used by the DFU bootloader (SDK 7.0+)..
	     * The Init packet for the bootloader must be placed in the .dat file.
	     * </p>
	     *
	     * @see #ExtraFileType
	     */
	    public const int TypeSoftDevice = 0x01;
	    /**
	     * <p>
	     * The file contains a new version of Bootloader.
	     * </p>
	     * <p>
	     * Since DFU Library 7.0 all firmware may contain an Init packet. The Init packet is required if Extended Init Packet is used by the DFU bootloader (SDK 7.0+).
	     * The Init packet for the bootloader must be placed in the .dat file.
	     * </p>
	     *
	     * @see #ExtraFileType
	     */
	    public const int TypeBootloader = 0x02;
	    /**
	     * <p>
	     * The file contains a new version of Application.
	     * </p>
	     * <p>
	     * Since DFU Library 0.5 all firmware may contain an Init packet. The Init packet is required if Extended Init Packet is used by the DFU bootloader (SDK 7.0+).
	     * The Init packet for the application must be placed in the .dat file.
	     * </p>
	     *
	     * @see #ExtraFileType
	     */
	    public const int TypeApplication = 0x04;
	    /**
	     * <p>
	     * A ZIP file that consists of more than 1 file. Since SDK 8.0 the ZIP Distribution packet is a recommended way of delivering firmware files. Please, see the DFU documentation for
	     * more details. A ZIP distribution packet may be created using the 'nrf utility' command line application, that is a part of Master Control Panel 3.8.0.
	     * For backwards compatibility this library supports also ZIP files without the manifest file. Instead they must follow the fixed naming convention:
	     * The names of files in the ZIP must be: <b>softdevice.hex</b> (or .bin), <b>bootloader.hex</b> (or .bin), <b>application.hex</b> (or .bin) in order
	     * to be read correctly. Using the Soft Device v7.0.0+ the Soft Device and Bootloader may be updated and sent together. In case of additional application file included,
	     * the service will try to send Soft Device, Bootloader and Application together (which is not supported currently) and if it fails, send first SD+BL, reconnect and send the application
	     * in the following connection.
	     * </p>
	     * <p>
	     * Since the DFU Library 0.5 you may specify the Init packet, that will be send prior to the firmware. The Init packet contains some verification data, like a device type and
	     * revision, application version or a list of supported Soft Devices. The Init packet is required if Extended Init Packet is used by the DFU bootloader (SDK 7.0+).
	     * In case of using the compatibility ZIP files the Init packet for the Soft Device and Bootloader must be in the 'system.dat' file while for the application
	     * in the 'application.dat' file (included in the ZIP). The CRC in the 'system.dat' must be a CRC of both BIN contents if both a Soft Device and a Bootloader is present.
	     * </p>
	     *
	     * @see #ExtraFileType
	     */
	    public const int TypeAuto = 0x00;
	    /**
	     * An extra field with progress and error information used in broadcast events.
	     */
	    public const string ExtraData = "No.NordicSemi.Doid.DFU.extra.ExtraData";
	    /**
	     * An extra field to send the progress or error information in the DFU notification. The value may contain:
	     * <ul>
	     * <li>Value 0 - 100 - percentage progress value</li>
	     * <li>One of the following status constants:
	     * <ul>
	     * <li>{@link #ProgressConnecting}</li>
	     * <li>{@link #ProgressStarting}</li>
	     * <li>{@link #ProgressEnablingDfuMode}</li>
	     * <li>{@link #ProgressValidating}</li>
	     * <li>{@link #ProgressDisconnecting}</li>
	     * <li>{@link #ProgressCompleted}</li>
	     * <li>{@link #ProgressAborted}</li>
	     * </ul>
	     * </li>
	     * <li>An error code with {@link #ErrorMask} if Initialization error occurred</li>
	     * <li>An error code with {@link #ErrorRemoteMask} if remote DFU target returned an error</li>
	     * <li>An error code with {@link #ErrorConnectionMask} if connection error occurred (f.e. GATT error (133) or Internal GATT Error (129))</li>
	     * </ul>
	     * To check if error occurred use:<br />
	     * {@code bool error = progressValue >= DfuBaseService.ErrorMask;}
	     */
	    public const string ExtraProgress = "No.NordicSemi.Doid.DFU.extra.ExtraProgress";
	    /**
	     * The number of currently transferred part. The SoftDevice and Bootloader may be send together as one part. If user wants to upload them together with an application it has to be sent
	     * in another connection as the second part.
	     *
	     * @see No.NordicSemi.Doid.DFU.DfuBaseService#ExtraPartsTotal
	     */
	    public const string ExtraPartCurrent = "No.NordicSemi.Doid.DFU.extra.ExtraPartCurrent";
	    /**
	     * Number of parts in total.
	     *
	     * @see No.NordicSemi.Doid.DFU.DfuBaseService#ExtraPartCurrent
	     */
	    public const string ExtraPartsTotal = "No.NordicSemi.Doid.DFU.extra.ExtraPartsTotal";
	    /**
	     * The current upload speed in bytes/millisecond.
	     */
	    public const string ExtraSpeedBPerMs = "No.NordicSemi.Doid.DFU.extra.ExtraSpeedBPerMs";
	    /**
	     * The average upload speed in bytes/millisecond for the current part.
	     */
	    public const string ExtraAvgSpeedBPerMs = "No.NordicSemi.Doid.DFU.extra.ExtraAvgSpeedBPerMs";
	    /**
	     * The broadcast message contains the following extras:
	     * <ul>
	     * <li>{@link #ExtraData} - the progress value (percentage 0-100) or:
	     * <ul>
	     * <li>{@link #ProgressConnecting}</li>
	     * <li>{@link #ProgressStarting}</li>
	     * <li>{@link #ProgressEnablingDfuMode}</li>
	     * <li>{@link #ProgressValidating}</li>
	     * <li>{@link #ProgressDisconnecting}</li>
	     * <li>{@link #ProgressCompleted}</li>
	     * <li>{@link #ProgressAborted}</li>
	     * </ul>
	     * </li>
	     * <li>{@link #ExtraDeviceAddress} - the target device address</li>
	     * <li>{@link #ExtraPartCurrent} - the number of currently transmitted part</li>
	     * <li>{@link #ExtraPartsTotal} - total number of parts that are being sent, f.e. if a ZIP file contains a Soft Device, a Bootloader and an Application,
	     * the SoftDevice and Bootloader will be send together as one part. Then the service will disconnect and reconnect to the new Bootloader and send the
	     * application as part number two.</li>
	     * <li>{@link #ExtraSpeedBPerMs} - current speed in bytes/millisecond as float</li>
	     * <li>{@link #ExtraAvgSpeedBPerMs} - the average transmission speed in bytes/millisecond as float</li>
	     * </ul>
	     */
	    public const string BroadcartProgress = "No.NordicSemi.Doid.DFU.broadcast.BroadcartProgress";
	    /**
	     * Service is connecting to the remote DFU target.
	     */
	    public const int ProgressConnecting = -1;
	    /**
	     * Service is enabling _notifications and Starting transmission.
	     */
	    public const int ProgressStarting = -2;
	    /**
	     * Service has triggered a switch to bootloader mode. Now the service waits for the link loss event (this may take up to several seconds) and will connect again
	     * to the same device, now Started in the bootloader mode.
	     */
	    public const int ProgressEnablingDfuMode = -3;
	    /**
	     * Service is sending validation request to the remote DFU target.
	     */
	    public const int ProgressValidating = -4;
	    /**
	     * Service is disconnecting from the DFU target.
	     */
	    public const int ProgressDisconnecting = -5;
	    /**
	     * The connection is successful.
	     */
	    public const int ProgressCompleted = -6;
	    /**
	     * The upload has been aborted. Previous software version will be restored on the target.
	     */
	    public const int ProgressAborted = -7;
	    /**
	     * The broadcast error message contains the following extras:
	     * <ul>
	     * <li>{@link #ExtraData} - the error number. Use {@link GattError#parse(int)} to get String representation</li>
	     * <li>{@link #ExtraDeviceAddress} - the target device address</li>
	     * </ul>
	     */
	    public const string BroadcastError = "No.NordicSemi.Doid.DFU.broadcast.BroadcastError";
	    /**
	     * The type of the error. This extra contains information about that kind of error has occurred. Connection state errors and other errors may share the same numbers.
	     * For example, the {@link BluetoothGattCallback#onCharacteristicWrite(BluetoothGatt, BluetoothGattCharacteristic, int)} method may return a status code 8 (GATT INSUF AUTHORIZATION),
	     * while the status code 8 returned by {@link BluetoothGattCallback#onConnectionStateChange(BluetoothGatt, int, int)} is a GATT CONN TIMEOUT error.
	     */
	    public const string ExtraErrorType = "No.NordicSemi.Doid.DFU.extra.ExtraErrorType";
	    public const int ErrorTypeOther = 0;
	    public const int ErrorTypeCommunicationState = 1;
	    public const int ErrorTypeCommunication = 2;
	    public const int ErrorTypeDfuRemote = 3;
	    /**
	     * If this bit is set than the progress value indicates an error. Use {@link GattError#parse(int)} to obtain error name.
	     */
	    public const int ErrorMask = 0x1000;
	    public const int ErrorDeviceDisconnected = ErrorMask; // | 0x00;
	    public const int ErrorFileNotFound = ErrorMask | 0x01;
	    /**
	     * Thrown if service was unable to open the file ({@link java.io.IOException} has been thrown).
	     */
	    public const int ErrorFileError = ErrorMask | 0x02;
	    /**
	     * Thrown then input file is not a valid HEX or ZIP file.
	     */
	    public const int ErrorFileInvalid = ErrorMask | 0x03;
	    /**
	     * Thrown when {@link java.io.IOException} occurred when reading from file.
	     */
	    public const int ErrorFileIOException = ErrorMask | 0x04;
	    /**
	     * Error thrown then {@code gatt.discoverServices();} returns false.
	     */
	    public const int ErrorServiceDiscoveryNotStarted = ErrorMask | 0x05;
	    /**
	     * Thrown when the service discovery has finished but the DFU service has not been found. The device does not support DFU of is not in DFU mode.
	     */
	    public const int ErrorServiceNotFound = ErrorMask | 0x06;
	    /**
	     * Thrown when the required DFU service has been found but at least one of the DFU characteristics is absent.
	     */
	    public const int ErrorCharacteristicsNotFound = ErrorMask | 0x07;
	    /**
	     * Thrown when unknown response has been obtained from the target. The DFU target must follow specification.
	     */
	    public const int ErrorInvalidResponse = ErrorMask | 0x08;
	    /**
	     * Thrown when the the service does not support given type or mime-type.
	     */
	    public const int ErrorFileTypeUnsupported = ErrorMask | 0x09;
	    /**
	     * Thrown when the the Bluetooth adapter is disabled.
	     */
	    public const int ErrorBluetoothDisabled = ErrorMask | 0x0A;
	    /**
	     * Flag set then the DFU target returned a DFU error. Look for DFU specification to get error codes.
	     */
	    public const int ErrorRemoteMask = 0x2000;
	    /**
	     * The flag set when one of {@link android.bluetooth.BluetoothGattCallback} methods was called with status other than {@link android.bluetooth.BluetoothGatt#GATT_SUCCESS}.
	     */
	    public const int ErrorConnectionMask = 0x4000;
	    /**
	     * The flag set when the {@link android.bluetooth.BluetoothGattCallback#onConnectionStateChange(android.bluetooth.BluetoothGatt, int, int)} method was called with
	     * status other than {@link android.bluetooth.BluetoothGatt#GATT_SUCCESS}.
	     */
	    public const int ErrorConnectionStateMask = 0x8000;
	    /**
	     * The log events are only broadcast when there is no nRF Logger installed. The broadcast contains 2 extras:
	     * <ul>
	     * <li>{@link #ExtraLogLevel} - The log level, one of following: {@link #LogLevelDebug}, {@link #LogLevelVerbose}, {@link #LogLevelInfo},
	     * {@link #LogLevelApplication}, {@link #LogLevelWarning}, {@link #LogLevelError}</li>
	     * <li>{@link #ExtraLogMessage}</li> - The log message
	     * </ul>
	     */
	    public const string BroadcastLog = "No.NordicSemi.Doid.DFU.broadcast.BroadcastLog";
	    public const string ExtraLogMessage = "No.NordicSemi.Doid.DFU.extra.EXTRA_LOG_INFO";
	    public const string ExtraLogLevel = "No.NordicSemi.Doid.DFU.extra.ExtraLogLevel";
	    /*
	     * Note:
	     * The nRF Logger API library has been excluded from the DfuLibrary.
	     * All log events are now being sent using local broadcasts and may be logged into nRF Logger in the app module.
	     * This is to make the Dfu module independent from logging tool.
	     *
	     * The log levels below are equal to log levels in nRF Logger API library, v 2.0.
	     * @see https://github.com/NordicSemiconductor/nRF-Logger-API
	     */
	    /**
	     * Level used just for debugging purposes. It has lowest level
	     */
	    public readonly static int LogLevelDebug = 0;
	    /**
	     * Log entries with minor importance
	     */
	    public readonly static int LogLevelVerbose = 1;
	    /**
	     * Default logging level for important entries
	     */
	    public readonly static int LogLevelInfo = 5;
	    /**
	     * Log entries level for applications
	     */
	    public readonly static int LogLevelApplication = 10;
	    /**
	     * Log entries with high importance
	     */
	    public readonly static int LogLevelWarning = 15;
	    /**
	     * Log entries with very high importance, like errors
	     */
	    public readonly static int LogLevelError = 20;
	    /**
	     * Activity may broadcast this broadcast in order to pause, resume or abort DFU process.
	     * Use {@link #ExtraAction} extra to pass the action.
	     */
	    public const string BroadcastAction = "No.NordicSemi.Doid.DFU.broadcast.BroadcastAction";
	    /**
	     * The action extra. It may have one of the following values: {@link #ActionPause}, {@link #ActionResume}, {@link #ActionAbort}.
	     */
	    public const string ExtraAction = "No.NordicSemi.Doid.DFU.extra.ExtraAction";
	    /** Pauses the upload. The service will wait for broadcasts with the action set to {@link #ActionResume} or {@link #ActionAbort}. */
	    public const int ActionPause = 0;
	    /** Resumes the upload that has been paused before using {@link #ActionPause}. */
	    public const int ActionResume = 1;
	    /**
	     * Aborts the upload. The service does not need to be paused before.
	     * After sending {@link #BroadcastAction} with extra {@link #ExtraAction} set to this value the DFU bootloader will restore the old application
	     * (if there was already an application). Be aware that uploading the Soft Device will erase the application in order to make space in the memory.
	     * In case there is no application, or the application has been removed, the DFU bootloader will be Started and user may try to send the application again.
	     * The bootloader may advertise with the address incremented by 1 to prevent caching services.
	     */
	    public const int ActionAbort = 2;

	    // DFU status values
	    public const int DfuStatusSuccess = 1;
	    public const int DfuStatusInvalidState = 2;
	    public const int DfuStatusNotSupported = 3;
	    public const int DfuStatusDataSizeExceedsLimit = 4;
	    public const int DfuStatusCrcError = 5;
	    public const int DfuStatusOperationFailed = 6;
	    // Operation codes and packets
	    private const byte _opCodeStartDfuKey = 0x01; // 1
	    private const byte _opCodeInitDfuParamsKey = 0x02; // 2
	    private const byte _opCodeReceiveFirmwareImageKey = 0x03; // 3
	    private const byte _opCodeValidateKey = 0x04; // 4
	    private const byte _opCodeActivateAndResetKey = 0x05; // 5
	    private const byte _opCodeResetKey = 0x06; // 6
	    //private const byte _opCodePacketReportReceivedImageSizeKey = 0x07; // 7
	    private const byte _opCodePacketReceiptNotIfReqKey = 0x08; // 8
	    private const byte _opCodeResponseCodeKey = 0x10; // 16
	    private const byte _opCodePacketReceiptNotifKey = 0x11; // 11
	    private const byte[] _opCodeStartDfu = new byte[] { _opCodeStartDfuKey, 0x00 };
	    private const byte[] _opCodeInitDfuParamsStart = new byte[] { _opCodeInitDfuParamsKey, 0x00 };
	    private const byte[] _opCodeInitDfuParamsComplete = new byte[] { _opCodeInitDfuParamsKey, 0x01 };
	    private const byte[] _opCodeReceiveFirmwareImage = new byte[] { _opCodeReceiveFirmwareImageKey };
	    private const byte[] _opCodeValidate = new byte[] { _opCodeValidateKey };
	    private const byte[] _opCodeActivateAndReset = new byte[] { _opCodeActivateAndResetKey };
	    private const byte[] _opCodeReset = new byte[] { _opCodeResetKey };
	    //private const byte[] _opCodeReportReceivedImageSize = new byte[] { _opCodePacketReportReceivedImageSizeKey };
	    private const byte[] _opCodePacketReceiptNotIfReq = new byte[] { _opCodePacketReceiptNotIfReqKey, 0x00, 0x00 };

	    // UUIDs used by the DFU
        private static long test = Convert.ToInt64(0x800000805F9B34FBL);
        private const UUID _genericAttributeServiceUuid = new UUID(0x0000180100001000L, Convert.ToInt64(0x800000805F9B34FBL));
        private const UUID _serviceChangedUuid = new UUID(0x00002A0500001000L, Convert.ToInt64(0x800000805F9B34FBL));
	    private const UUID _dfuServiceUuid = new UUID(0x000015301212EFDEL, 0x1523785FEABCD123L);
	    private const UUID _duControlPointUuid = new UUID(0x000015311212EFDEL, 0x1523785FEABCD123L);
	    private const UUID _dfuPacketUuid = new UUID(0x000015321212EFDEL, 0x1523785FEABCD123L);
	    private const UUID _dfuVersion = new UUID(0x000015341212EFDEL, 0x1523785FEABCD123L);
	    private const UUID _clientCharacteristicConfig = new UUID(0x0000290200001000L, Convert.ToInt64(0x800000805f9b34fbL));
	    //
	    public const int NotificationId = 283; // a random number
	    private const int _notifications = 1;
	    private const int _indications = 2;
	    private const char[] _hexArray = "0123456789ABCDEF".ToCharArray();
	    private const int _maxPacketSize = 20; // the maximum number of bytes in one packet is 20. May be less.
	    private readonly byte[] _buffer = new byte[_maxPacketSize];
	    /**
	     * Lock used in synchronization purposes
	     */
	    //private readonly Java.Lang.Object _lock = new Java.Lang.Object();
	    private BluetoothAdapter _bluetoothAdapter;
	    private InputStream _inputStream;
	    private string _deviceAddress;
	    private string _deviceName;
	    /**
	     * The current connection state. If its value is > 0 than an error has occurred. Error number is a negative value of _connectionState
	     */
	    private int _connectionState;
	    public readonly static int StateDisconnected = 0;
	    public readonly static int StateConnecting = -1;
	    public readonly static int StateConnected = -2;
	    public readonly static int StateConnectedAndReady = -3; // indicates that services were discovered
	    public readonly static int StateDisconnecting = -4;
	    public readonly static int StateClosed = -5;
	    /**
	     * The number of the last error that has occurred or 0 if there was no error
	     */
	    private int _error;
	    /**
	     * Flag set when we got confirmation from the device that notifications are enabled.
	     */
	    private bool _notificationsEnabled;
	    /**
	     * Flag set when we got confirmation from the device that Service Changed indications are enabled.
	     */
	    private bool _serviceChangedIndicationsEnabled;
	    /**
	     * The number of packets of firmware data to be send before receiving a new Packets receipt notification. 0 disables the packets _notifications
	     */
	    private int _packetsBeforeNotification = 10;
	    /**
	     * Size of BIN content of all hex files that are going to be transmitted.
	     */
	    private int _imageSizeInBytes;
	    /**
	     * Number of bytes transmitted.
	     */
	    private int _bytesSent;
	    /**
	     * Number of bytes confirmed by the notification.
	     */
	    private int _bytesConfirmed;
	    private int _packetsSentSinceNotification;
	    /**
	     * This value is used to calculate the current transfer speed.
	     */
	    private int _lastBytesSent;
	    /**
	     * Firmware update may require two connections: one for Soft Device and/or Bootloader upload and second for Application. This fields contains the current part number.
	     */
	    private int _partCurrent;
	    /**
	     * Total number of parts.
	     */
	    private int _partsTotal;
	    private int _fileType;
	    private long _lastProgressTime, mStartTime;
	    /**
	     * Flag sent when a request has been sent that will cause the DFU target to Reset. Often, after sending such command, Android throws a connection state error. If this flag is set the error will be
	     * ignored.
	     */
	    private bool _resetRequestSent;
	    /**
	     * Flag indicating whether the image size has been already transferred or not
	     */
	    private bool _imageSizeSent;
	    /**
	     * Flag indicating whether the Init packet has been already transferred or not
	     */
	    private bool _initPacketSent;
	    /**
	     * Flag indicating whether the request was Completed or not
	     */
	    private bool _requestCompleted;
	    /**
	     * <p>
	     * Flag set to <code>true</code> when the DFU target had send any notification with status other than {@link #DfuStatusSuccess}. Setting it to <code>true</code> will abort sending firmware and
	     * stop logging _notifications (read below for explanation).
	     * </p>
	     * <p>
	     * The onCharacteristicWrite(..) callback is written when Android puts the packet to the outgoing queue, not when it physically send the data. Therefore, in case of invalid state of the DFU
	     * target, Android will first put up to N* packets, one by one, while in fact the first will be transmitted. In case the DFU target is in an invalid state it will notify Android with a
	     * notification 10-03-02 for each packet of firmware that has been sent. However, just after receiving the first one this service will try to send the Reset command while still getting more
	     * 10-03-02 _notifications. This flag will prevent from logging "Notification received..." more than once.
	     * </p>
	     * <p>
	     * Additionally, sometimes after writing the command 6 ({@link #OpCodeReset}), Android will receive a notification and update the characteristic value with 10-03-02 and the callback for write
	     * Reset command will log "[DFU] Data written to ..., value (0x): 10-03-02" instead of "...(x0): 06". But this does not matter for the DFU process.
	     * </p>
	     * <p>
	     * N* - Value of Packet Receipt Notification, 10 by default.
	     * </p>
	     */
	    private bool _remoteErrorOccurred;
	    private bool _paused;
	    private bool _aborted;

	    /**
	     * Latest data received from device using notification.
	     */
	    private byte[] _receivedData = null;

	    private readonly BroadcastReceiver _connectionStateBroadcastReceiver;

	    private readonly BroadcastReceiver _dfuActionReceiver;

	    private BroadcastReceiver _bondStateBroadcastReceiver;

	    private GattCallback _gattCallback = new GattCallback();

        private async void _gattCallback_DeviceConnectionStateChanged(object sender, DeviceConnectionStateChangeEventArgs e)
        {
 	        // Check whether an error occurred
			if (e.Status == GattStatus.Success) 
            {
				if (e.NewState == ProfileState.Connected) {
					Log.Info("DfuBaseService", "Connected to GATT server");
					_connectionState = StateConnected;

					/*
					    *  The onConnectionStateChange callback is called just after establishing connection and before sending Encryption Request BLE event in case of a paired device. 
					    *  In that case and when the Service Changed CCCD is enabled we will get the indication after initializing the encryption, about 1600 milliseconds later. 
					    *  If we discover services right after connecting, the onServicesDiscovered callback will be called immediately, before receiving the indication and the following 
					    *  service discovery and we may end up with old, application's services instead.
					    *  
					    *  This is to support the buttonless switch from application to bootloader mode where the DFU bootloader notifies the master about service change.
					    *  Tested on Nexus 4 (Android 4.4.4 and 5), Nexus 5 (Android 5), Samsung Note 2 (Android 4.4.2). The time after connection to end of service discovery is about 1.6s 
					    *  on Samsung Note 2.
					    *  
					    *  NOTE: We are doing this to avoid the hack with calling the hidden gatt.refresh() method, at least for bonded devices.
					    */
					if (e.Gatt.Device.BondState == Bond.Bonded) 
                    {
						Log.Debug("DfuBaseService", "Waiting 1600 ms for a possible Service Changed indication...");
                        await Task.Delay(1600);

						// After 1.6s the services are already discovered so the following gatt.discoverServices() finishes almost immediately.

						// NOTE: This also works with shorted waiting time. The gatt.discoverServices() must be called after the indication is received which is
						// about 600ms after establishing connection. Values 600 - 1600ms should be OK.
					}

					// Attempts to discover services after successful connection.
					bool success = e.Gatt.DiscoverServices();
					Log.Info("DfuBaseService", "Attempting to start service discovery... " + (success ? "succeed" : "failed"));

					if (!success) {
						_error = ErrorServiceDiscoveryNotStarted;
					} else {
						// Just return here, lock will be notified when service discovery finishes
						return;
					}
				} else if (e.NewState == ProfileState.Disconnected) {
					Log.Info("DfuBaseService", "Disconnected from GATT server");
					_paused = false;
					_connectionState = StateDisconnected;
				}
			} else {
				Log.Error("DfuBaseService", "Connection state change error: " + e.Status + " newState: " + e.NewState);
				_paused = false;
				_error = ErrorConnectionStateMask | (int)e.Status;
			}
        }

        private void _gattCallback_DescriptorWrite(object sender, BluetoothGattDescriptorEventArgs e)
        {
 	        throw new NotImplementedException();
        }

        private void _gattCallback_DescriptorRead(object sender, BluetoothGattDescriptorEventArgs e)
        {
            if (e.Status == GattStatus.Success) 
            {
				if (_clientCharacteristicConfig.Equals(e.Descriptor.Uuid)) 
                {
					if (_serviceChangedUuid.Equals(e.Descriptor.Characteristic.Uuid)) 
                    {
						// We have enabled indications for the Service Changed characteristic
						_serviceChangedIndicationsEnabled = e.Descriptor.GetValue()[0] == 2;
						_requestCompleted = true;
					}
				}
			} 
            else
            {
				Log.Error("DfuBaseService", "Descriptor read error: " + e.Status);
				_error = ErrorConnectionMask | (int)e.Status;
			}

			// Notify waiting thread
			//synchronized (_lock) {
			//	_lock.notifyAll();
			//}
        }

        private void _gattCallback_CharacteristicWrite(object sender, BluetoothGattDescriptorEventArgs e)
        {
 	        if (e.Status == GattStatus.Success)
            {
				if (_clientCharacteristicConfig.Equals(e.Descriptor.Uuid)) 
                {
					if (_serviceChangedUuid.Equals(e.Descriptor.Characteristic.Uuid)) 
                    {
						// We have enabled indications for the Service Changed characteristic
						_serviceChangedIndicationsEnabled = e.Descriptor.GetValue()[0] == 2;
					}
                    else 
                    {
                        // We have enabled notifications for this characteristic
						_notificationsEnabled = e.Descriptor.GetValue()[0] == 1;
					}
				}
			} 
            else 
            {
				Log.Error("DfuBaseService", "Descriptor write error: " + e.Status);
				_error = ErrorConnectionMask | (int)e.Status;
			}

			// Notify waiting thread
			//synchronized (_lock) {
			//	_lock.notifyAll();
			//}
        }

        private void _gattCallback_CharacteristicRead(object sender, BluetoothGattCharacteristicEventArgs e)
        {
 	        if (e.Status == GattStatus.Success) 
            {
			    /*
				* This method is called when the DFU Version characteristic has been read.
				*/
			    sendLogBroadcast(LogLevelInfo, "Read Response received from " + e.Characteristic.Uuid + ", value (0x): " + parse(e.Characteristic));
			    _receivedData = e.Characteristic.GetValue();
			    _requestCompleted = true;
		    } 
            else
            {
			    Log.Error("DfuBaseService", "Characteristic read error: " + e.Status);
			    _error = ErrorConnectionMask | (int)e.Status;
		    }

		    // Notify waiting thread
		    //synchronized (_lock) {
			//    _lock.notifyAll();
		    //}
        }

        private void _gattCallback_CharacteristicWrite(object sender, BluetoothGattCharacteristicEventArgs e)
        {
            if (e.Status == GattStatus.Success)
            {
                /*
                 * This method is called when either a CONTROL POINT or PACKET characteristic has been written.
                 * If it is the CONTROL POINT characteristic, just set the {@link _requestCompleted} flag to true. The main thread will continue its task when notified.
                 * If the PACKET characteristic was written we must:
                 * - if the image size was written in DFU Start procedure, just set flag to true
                 * otherwise
                 * - send the next packet, if notification is not required at that moment, or
                 * - do nothing, because we have to wait for the notification to confirm the data received
                 */
                if (_dfuPacketUuid.Equals(e.Characteristic.Uuid)) 
                {
                    if (_imageSizeSent && _initPacketSent) 
                    {
                        // If the PACKET characteristic was written with image data, update counters
                        _bytesSent += e.Characteristic.GetValue().Length;
                        _packetsSentSinceNotification++;

                        // If a packet receipt notification is expected, or the last packet was sent, do nothing. There onCharacteristicChanged listener will catch either
                        // a packet confirmation (if there are more bytes to send) or the image received notification (it upload process was completed)
                        bool notificationExpected = _packetsBeforeNotification > 0 && _packetsSentSinceNotification == _packetsBeforeNotification;
                        bool lastPacketTransferred = _bytesSent == _imageSizeInBytes;

                        if (notificationExpected || lastPacketTransferred)
                            return;

                        // When neither of them is true, send the next packet
                        try 
                        {
                            waitIfPaused();
                            // The writing might have been aborted (_aborted = true), an error might have occurred.
                            // In that case stop sending.
                            if (_aborted || _error != 0 || _remoteErrorOccurred || _resetRequestSent) 
                            {
                                // notify waiting thread
                                //synchronized (_lock) 
                                //{
                                //    sendLogBroadcast(LogLevelWarning, "Upload terminated");
                                //    _lock.notifyAll();
                                //    return;
                                //}
                            }

                            byte[] buffer = _buffer;
                            int size = _inputStream.Read(buffer);
                            writePacket(e.Gatt, e.Characteristic, buffer, size);
                            updateProgressNotification();
                            return;
                        } 
                        catch (HexFileValidationException ex) 
                        {
                            Log.Error("DfuBaseService", "Invalid HEX file");
                            _error = ErrorFileInvalid;
                        } 
                        catch (IOException ex) 
                        {
                            Log.Error("DfuBaseService", "Error while reading the input stream", ex);
                            _error = ErrorFileIOException;
                        }
                    } 
                    else if (!_imageSizeSent) 
                    {
                        // We've got confirmation that the image size was sent
                        sendLogBroadcast(LogLevelInfo, "Data written to " + e.Characteristic.Uuid + ", value (0x): " + parse(e.Characteristic));
                        _imageSizeSent = true;
                    }
                    else 
                    {
                        // We've got confirmation that the init packet was sent
                        sendLogBroadcast(LogLevelInfo, "Data written to " + e.Characteristic.Uuid + ", value (0x): " + parse(e.Characteristic));
                        _initPacketSent = true;
                    }
                } 
                else 
                {
                    // If the CONTROL POINT characteristic was written just set the flag to true. The main thread will continue its task when notified.
                    sendLogBroadcast(LogLevelInfo, "Data written to " + e.Characteristic.Uuid + ", value (0x): " + parse(e.Characteristic));
                    _requestCompleted = true;
                }
            } 
            else 
            {
                /*
                 * If a Reset (Op Code = 6) or Activate and Reset (Op Code = 5) commands are sent, the DFU target resets and sometimes does it so quickly that does not manage to send
                 * any ACK to the controller and error 133 is thrown here. This bug should be fixed in SDK 8.0+ where the target would gracefully disconnect before restarting.
                 */
                if (_resetRequestSent)
                {
                    _requestCompleted = true;
                }
                else
                {
                    Log.Error("DfuBaseService", "Characteristic write error: " + e.Status);
                    _error = ErrorConnectionMask | (int)e.Status;
                }
            }

            // Notify waiting thread
            //synchronized (_lock) {
            //    _lock.notifyAll();
            //}
        }

        private void _gattCallback_CharacteristicChanged(object sender, BluetoothGattCharacteristicEventArgs e)
        {
            var responseType = (int)e.Characteristic.GetIntValue(GattFormat.Uint8, 0);

            switch (responseType) 
            {
                case _opCodePacketReceiptNotifKey:
                    BluetoothGattCharacteristic packetCharacteristic = e.Gatt.GetService(_dfuServiceUuid).GetCharacteristic(_dfuPacketUuid);
                    try 
                    {
                        _bytesConfirmed = (int)e.Characteristic.GetIntValue(GattFormat.Uint32, 1);
                        _packetsSentSinceNotification = 0;

                        waitIfPaused();
                        // The writing might have been aborted (_aborted = true), an error might have occurred.
                        // In that case quit sending.
                        if (_aborted || _error != 0 || _remoteErrorOccurred || _resetRequestSent) {
                            sendLogBroadcast(LogLevelWarning, "Upload terminated");
                            break;
                        }

                        byte[] buffer = _buffer;
                        int size = _inputStream.Read(buffer);
                        writePacket(e.Gatt, packetCharacteristic, buffer, size);
                        updateProgressNotification();
                        return;
                    }
                    catch (HexFileValidationException ex) 
                    {
                        Log.Error("DfuBaseService", "Invalid HEX file");
                        _error = ErrorFileInvalid;
                    } 
                    catch (IOException ex) 
                    {
                        Log.Error("DfuBaseService", "Error while reading the input stream", ex);
                        _error = ErrorFileIOException;
                    }
                    break;
                case _opCodeResponseCodeKey:
                default:
                    /*
                     * If the DFU target device is in invalid state (f.e. the Init Packet is required but has not been selected), the target will send DFU_STATUS_INVALID_STATE error
                     * for each firmware packet that was send. We are interested may ignore all but the first one.
                     * After obtaining a remote DFU error the _opCodeResetKey will be sent.
                     */
					if (_remoteErrorOccurred)
						break;
                    var status = (int)e.Characteristic.GetIntValue(GattFormat.Uint8, 2);
					if (status != DfuStatusSuccess)
						_remoteErrorOccurred = true;

					sendLogBroadcast(LogLevelInfo, "Notification received from " + e.Characteristic.Uuid + ", value (0x): " + parse(e.Characteristic));
					_receivedData = e.Characteristic.GetValue();
					break;
			    }

			    // Notify waiting thread
			    //synchronized (_lock) {
			    //    _lock.notifyAll();
			    //}
            }

        private void _gattCallback_ServicesDiscovered(object sender, GattEventArgs e)
        {
 	        if (e.Status == GattStatus.Success) 
            {
				Log.Info("DfuBaseService", "Services discovered");
				_connectionState = StateConnectedAndReady;
			} 
            else
            {
				Log.Error("DfuBaseService", "Service discovery error: " + e.Status);
				_error = ErrorConnectionMask | (int)e.Status;
			}

			// Notify waiting thread
			//synchronized (_lock) {
			//	_lock.notifyAll();
			//}
        }

        private string parse(BluetoothGattCharacteristic characteristic) 
        {
			byte[] data = characteristic.GetValue();
			if (data == null)
				return "";
			int length = data.Length;
			if (length == 0)
				return "";

			char[] charOut = new char[length * 3 - 1];
			for (int j = 0; j < length; j++) {
				int v = data[j] & 0xFF;
				charOut[j * 3] = _hexArray[(int)((uint)v >> 4)];
				charOut[j * 3 + 1] = _hexArray[v & 0x0F];
				if (j != length - 1)
					charOut[j * 3 + 2] = '-';
			}
            return new string(charOut);
		}

	    /**
	     * Stores the last progress percent. Used to prevent from sending progress notifications with the same value.
	     * @see #updateProgressNotification(int)
	     */
	    private int _lastProgress = -1;

	    public DfuBaseService() : base()
        {
		    //super(TAG);
            _connectionStateBroadcastReceiver = new ConnectionStateBroadcastReceiver(ref _deviceAddress, ref _connectionState);
            _dfuActionReceiver = new DfuActionReceiver(ref _paused, ref _aborted);
            _bondStateBroadcastReceiver = new BondStateBroadcastReceiver(ref _deviceAddress, ref _requestCompleted);

            _gattCallback.CharacteristicChanged +=_gattCallback_CharacteristicChanged;
            _gattCallback.CharacteristicRead +=_gattCallback_CharacteristicRead;
            _gattCallback.CharacteristicWrite += _gattCallback_CharacteristicWrite;
            _gattCallback.DescriptorWrite += _gattCallback_DescriptorWrite;
            _gattCallback.DescriptorRead += _gattCallback_DescriptorRead;
            _gattCallback.DeviceConnectionStateChanged +=_gattCallback_DeviceConnectionStateChanged;
            _gattCallback.ServicesDiscovered +=_gattCallback_ServicesDiscovered;
	    }

	    private static IntentFilter makeDfuActionIntentFilter() {
		    IntentFilter intentFilter = new IntentFilter();
		    intentFilter.AddAction(DfuBaseService.BroadcastAction);
		    return intentFilter;
	    }

	    public override void OnCreate() 
        {
		    base.OnCreate();

		    initialize();

		    LocalBroadcastManager manager = LocalBroadcastManager.GetInstance(this);
		    IntentFilter actionFilter = makeDfuActionIntentFilter();
		    manager.RegisterReceiver(_dfuActionReceiver, actionFilter);
		    RegisterReceiver(_dfuActionReceiver, actionFilter); // Additionally we must register this receiver as a non-local to get broadcasts from the notification actions

		    IntentFilter filter = new IntentFilter(BluetoothDevice.ActionAclDisconnected);
		    RegisterReceiver(_connectionStateBroadcastReceiver, filter);

		    IntentFilter bondFilter = new IntentFilter(BluetoothDevice.ActionBondStateChanged);
		    RegisterReceiver(_bondStateBroadcastReceiver, bondFilter);
	    }

	    public override void OnDestroy() 
        {
		    base.OnDestroy();

		    LocalBroadcastManager manager = LocalBroadcastManager.GetInstance(this);
		    manager.UnregisterReceiver(_dfuActionReceiver);

		    UnregisterReceiver(_dfuActionReceiver);
		    UnregisterReceiver(_connectionStateBroadcastReceiver);
		    UnregisterReceiver(_bondStateBroadcastReceiver);
	    }

	    protected override async void OnHandleIntent(Intent intent) 
        {
		    var preferences = PreferenceManager.GetDefaultSharedPreferences(this);

		    // Read input parameters
		    var deviceAddress = intent.GetStringExtra(ExtraDeviceAddress);
		    var deviceName = intent.GetStringExtra(ExtraDeviceName);
		    var filePath = intent.GetStringExtra(ExtraFilePath);
		    var fileUri = (Android.Net.Uri)intent.GetParcelableExtra(ExtraFileUri);
		    var initFilePath = intent.GetStringExtra(ExtraInitFilePath);
		    var initFileUri = (Android.Net.Uri)intent.GetParcelableExtra(ExtraInitFileUri);
		    var fileType = intent.GetIntExtra(ExtraFileType, TypeAuto);
		    if (filePath != null && fileType == TypeAuto)
			    fileType = filePath.ToLower().EndsWith("zip") ? TypeAuto : TypeApplication;
		    var mimeType = intent.GetStringExtra(ExtraFileMimeType);
		    mimeType = mimeType != null ? mimeType : (fileType == TypeAuto ? MimeTypeZip : MimeTypeOctetStream);
		    _partCurrent = intent.GetIntExtra(ExtraPartCurrent, 1);
		    _partsTotal = intent.GetIntExtra(ExtraPartsTotal, 1);

		    // Check file type and mime-type
		    if ((fileType & ~(TypeSoftDevice | TypeBootloader | TypeApplication)) > 0 || !(MimeTypeZip.Equals(mimeType) || MimeTypeOctetStream.Equals(mimeType))) 
            {
			    Log.Warn("DfuBaseService", "File type or file mime-type not supported");
			    sendLogBroadcast(LogLevelWarning, "File type or file mime-type not supported");
			    sendErrorBroadcast(ErrorFileTypeUnsupported);
			    return;
		    }
		    if (MimeTypeOctetStream.Equals(mimeType) && fileType != TypeSoftDevice && fileType != TypeBootloader && fileType != TypeApplication) {
			    Log.Warn("DfuBaseService", "Unable to determine file type");
			    sendLogBroadcast(LogLevelWarning, "Unable to determine file type");
			    sendErrorBroadcast(ErrorFileTypeUnsupported);
			    return;
		    }

		    _deviceAddress = deviceAddress;
		    _deviceName = deviceName;
		    _connectionState = StateDisconnected;
		    _bytesSent = 0;
		    _bytesConfirmed = 0;
		    _packetsSentSinceNotification = 0;
		    _error = 0;
		    _lastProgressTime = 0;
		    _aborted = false;
		    _paused = false;
		    _notificationsEnabled = false;
		    _resetRequestSent = false;
		    _requestCompleted = false;
		    _imageSizeSent = false;
		    _remoteErrorOccurred = false;

		    // Read preferences
		    bool packetReceiptNotificationEnabled = preferences.GetBoolean(DfuSettingsConstants.SettingsPacketReceiptNotificationEnabled, true);
		    String value = preferences.GetString(DfuSettingsConstants.SettingsNumberOfPackets, DfuSettingsConstants.SettingsNnumberOfPacketsDefault.ToString());
		    int numberOfPackets;
		    try {
			    numberOfPackets = int.Parse(value);
			    if (numberOfPackets < 0 || numberOfPackets > 0xFFFF)
				    numberOfPackets = DfuSettingsConstants.SettingsNnumberOfPacketsDefault;
		    } catch (NumberFormatException e) {
			    numberOfPackets = DfuSettingsConstants.SettingsNnumberOfPacketsDefault;
		    }
		    if (!packetReceiptNotificationEnabled)
			    numberOfPackets = 0;
		    _packetsBeforeNotification = numberOfPackets;
		    // The Soft Device starts where MBR ends (by default from the address 0x1000). Before there is a MBR section, which should not be transmitted over DFU.
		    // Applications and bootloader starts from bigger address. However, in custom DFU implementations, user may want to transmit the whole whole data, even from address 0x0000.
		    value = preferences.GetString(DfuSettingsConstants.SettingsMbrSize, DfuSettingsConstants.SettingsDefaultMbrSize.ToString());
		    int mbrSize;
		    try {
			    mbrSize = int.Parse(value);
			    if (mbrSize < 0)
				    mbrSize = 0;
		    } catch (NumberFormatException e) {
			    mbrSize = DfuSettingsConstants.SettingsDefaultMbrSize;
		    }

		    sendLogBroadcast(LogLevelVerbose, "Starting DFU service");

		    /*
		     * First the service is trying to read the firmware and init packet files.
		     *
		    InputStream is = null;
		    InputStream initIs = null;
		    int imageSizeInBytes;
		    try 
            {
			    // Prepare data to send, calculate stream size
			    try 
                {
				    sendLogBroadcast(LogLevelVerbose, "Opening file...");
				    if (fileUri != null) {
					    is = openInputStream(fileUri, mimeType, mbrSize, fileType);
				    } else {
					    is = openInputStream(filePath, mimeType, mbrSize, fileType);
				    }

				    if (initFileUri != null) {
					    // Try to read the Init Packet file from URI
					    initIs = getContentResolver().openInputStream(initFileUri);
				    } else if (initFilePath != null) {
					    // Try to read the Init Packet file from path
					    initIs = new FileInputStream(initFilePath);
				    }

				    mInputStream = is;
				    imageSizeInBytes = _imageSizeInBytes = is.available();
				    // Update the file type bit field basing on the ZIP content
				    if (fileType == TYPE_AUTO && MIME_TYPE_ZIP.equals(mimeType)) {
					    ArchiveInputStream zhis = (ArchiveInputStream) is;
					    fileType = zhis.getContentType();
				    }
				    _fileType = fileType;
				    // Set the Init packet stream in case of a ZIP file
				    if (MIME_TYPE_ZIP.equals(mimeType)) {
					    ArchiveInputStream zhis = (ArchiveInputStream) is;
					    if (fileType == TypeApplication) {
						    if (zhis.getApplicationInit() != null)
							    initIs = new ByteArrayInputStream(zhis.getApplicationInit());
					    } else {
						    if (zhis.getSystemInit() != null)
							    initIs = new ByteArrayInputStream(zhis.getSystemInit());
					    }
				    }
				    sendLogBroadcast(LogLevelInfo, "Image file opened (" + _imageSizeInBytes + " bytes in total)");
			    } catch (SecurityException e) {
				    Log.Error("DfuBaseService", "A security exception occurred while opening file", e);
				    updateProgressNotification(ErrorFileNotFound);
				    return;
			    } catch (FileNotFoundException e) {
				    Log.Error("DfuBaseService", "An exception occurred while opening file", e);
				    updateProgressNotification(ErrorFileNotFound);
				    return;
			    } catch (IOException e) {
				    Log.Error("DfuBaseService", "An exception occurred while calculating file size", e);
				    updateProgressNotification(ErrorFileError);
				    return;
			    }*/

			    /*
			     * Now let's connect to the device.
			     * All the methods below are synchronous. The _lock object is used to wait for asynchronous calls.
			     *
			    sendLogBroadcast(LogLevelVerbose, "Connecting to DFU target...");
			    updateProgressNotification(ProgressConnecting);

			    BluetoothGatt gatt = connect(deviceAddress);
			    // Are we connected?
			    if (gatt == null) {
				    Log.Error("DfuBaseService", "Bluetooth adapter disabled");
				    sendLogBroadcast(LogLevelError, "Bluetooth adapter disabled");
				    updateProgressNotification(ErrorBluetoothDisabled);
				    return;
			    }
			    if (_error > 0) { // error occurred
				    int error = _error & ~ErrorConnectionStateMask;
				    Log.Error("DfuBaseService", "An error occurred while connecting to the device:" + error);
				    sendLogBroadcast(LogLevelError, String.Format("Connection failed (0x%02X): %s", error, GattError.ParseConnectionError(error)));
				    terminateConnection(gatt, _error);
				    return;
			    }
			    if (_aborted) {
				    Log.Info("DfuBaseService", "Upload aborted");
				    sendLogBroadcast(LogLevelWarning, "Upload aborted");
				    terminateConnection(gatt, ProgressAborted);
				    return;
			    }

			    // We have connected to DFU device and services are discoverer
			    BluetoothGattService dfuService = gatt.GetService(_dfuServiceUuid); // there was a case when the service was null. I don't know why
			    if (dfuService == null) 
                {
				    Log.Error("DfuBaseService", "DFU service does not exists on the device");
				    sendLogBroadcast(LogLevelWarning, "Connected. DFU Service not found");
				    terminateConnection(gatt, ErrorServiceNotFound);
				    return;
			    }
			    BluetoothGattCharacteristic controlPointCharacteristic = dfuService.GetCharacteristic(_duControlPointUuid);
			    BluetoothGattCharacteristic packetCharacteristic = dfuService.GetCharacteristic(_dfuPacketUuid);
			    if (controlPointCharacteristic == null || packetCharacteristic == null) 
                {
				    Log.Error("DfuBaseService", "DFU characteristics not found in the DFU service");
				    sendLogBroadcast(LogLevelWarning, "Connected. DFU Characteristics not found");
				    terminateConnection(gatt, ErrorCharacteristicsNotFound);
				    return;
			    }
			    /*
			     * The DFU Version characteristic has been added in SDK 7.0.
			     *
			     * It may return version number in 2 bytes (f.e. 0x05-00), where the first one is the minor version and the second one is the major version.
			     * In case of 0x05-00 the DFU has the version 0.5.
			     *
			     * Currently the following version numbers are supported:
			     *
			     *   - 0.1 (0x01-00) - The service is connected to the device in application mode, not to the DFU Bootloader. The application supports Long Term Key (LTK)
			     *                     sharing and buttonless update. Enable notifications on the DFU Control Point characteristic and write 0x01-04 into it to jump to the Bootloader.
			     *                     Check the Bootloader version again for more info about the Bootloader version.
			     *
			     *   - 0.5 (0x05-00) - The device is in the OTA-DFU Bootloader mode. The Bootloader supports LTK sharing and requires the Extended Init Packet. It supports
			     *                     a SoftDevice, Bootloader or an Application update. SoftDevice and a Bootloader may be sent together.
			     *
			     *   - 0.6 (0x06-00) - The device is in the OTA-DFU Bootloader mode. The DFU Bootloader is from SDK 8.0 and has the same features as version 0.5. It also
			     *                     supports also sending Service Changed notification in application mode after successful or aborted upload so no refreshing services is required.
			     *
			    BluetoothGattCharacteristic versionCharacteristic = dfuService.GetCharacteristic(_dfuVersion); // this may be null for older versions of the Bootloader

			    sendLogBroadcast(LogLevelInfo, "Connected. Services discovered");
			    try 
                {
				    updateProgressNotification(ProgressStarting);

				    // Read the version number if available. The version number consists of 2 bytes: major and minor. Therefore f.e. the version 5 (00-05) can be read as 0.5.
				    int version = 0;
				    if (versionCharacteristic != null) 
                    {
					    version = readVersion(gatt, versionCharacteristic);
					    int minor = (version & 0x0F);
					    int major = (version >> 8);
					    Log.Info("DfuBaseService", "Version number read: " + major + "." + minor);
					    sendLogBroadcast(LogLevelApplication, "Version number read: " + major + "." + minor);
				    }

				    /*
				     *  Check if we are in the DFU Bootloader or in the Application that supports the buttonless update.
				     *
				     *  In the DFU from SDK 6.1, which was also supporting the buttonless update, there was no DFU Version characteristic. In that case we may find out whether
				     *  we are in the bootloader or application by simply checking the number of characteristics.
				     *
				    if (version == 1 || (version == 0 && gatt.Services.Count > 3)) // No DFU Version char but more services than Generic Access, Generic Attribute, DFU Service  
                    {
					    // The service is connected to the application, not to the bootloader
					    Log.Warn("DfuBaseService", "Application with buttonless update found");
					    sendLogBroadcast(LogLevelWarning, "Application with buttonless update found");

					    // If we are bonded we may want to enable Service Changed characteristic indications.
					    // Note: This feature will be introduced in the SDK 8.0 as this is the proper way to refresh attribute list on the phone.
					    bool hasServiceChanged = false;
					    if (gatt.Device.BondState == Bond.Bonded)
                        {
						    BluetoothGattService genericAttributeService = gatt.GetService(_genericAttributeServiceUuid);
						    if (genericAttributeService != null) 
                            {
							    BluetoothGattCharacteristic serviceChangedCharacteristic = genericAttributeService.GetCharacteristic(_serviceChangedUuid);
							    if (serviceChangedCharacteristic != null) 
                                {
								    // Let's read the current value of the Service Changed CCCD
								    bool serviceChangedIndicationsEnabled = isServiceChangedCCCDEnabled(gatt, serviceChangedCharacteristic);

								    if (!serviceChangedIndicationsEnabled) 
                                    {
									    enableCCCD(gatt, serviceChangedCharacteristic, INDICATIONS);
									    sendLogBroadcast(LogLevelApplication, "Service Changed indications enabled");

									    /*
									     * NOTE: The DFU Bootloader from SDK 8.0 (v0.6 and 0.5) has the following issue:
									     *
									     * When the central device (phone) connects to a bonded device (or connects and bonds) which supports the Service Changed characteristic,
									     * but does not have the Service Changed indications enabled, the phone must enable them, disconnect and reconnect before starting the
									     * DFU operation. This is because the current version of the Soft Device saves the ATT table on the DISCONNECTED event.
									     * Sending the "jump to Bootloader" command (0x01-04) will cause the disconnect followed be a reset. The Soft Device does not
									     * have time to store the ATT table on Flash memory before the reset.
									     *
									     * This applies only if:
									     * - the device was bonded before an upgrade,
									     * - the Application or the Bootloader is upgraded (upgrade of the Soft Device will erase the bond information anyway),
									     *     - Application:
									      *        if the DFU Bootloader has been modified and compiled to preserve the LTK and the ATT table after application upgrade (at least 2 pages)
									     *         See: \Nordic\nrf51\components\libraries\bootloader_dfu\dfu_types.h, line 56:
									     *          #define DFU_APP_DATA_RESERVED           0x0000  ->  0x0800+   //< Size of Application Data that must be preserved between application updates...
									     *     - Bootloader:
									     *         The Application memory should not be removed when the Bootloader is upgraded, so the Bootloader configuration does not matter.
									     *
									     * If the bond information is not to be preserved between the old and new applications, we may skip this disconnect/reconnect process.
									     * The DFU Bootloader will send the SD indication anyway when we will just continue here, as the information whether it should send it or not it is not being
									     * read from the application's ATT table, but rather passed as an argument of the "reboot to bootloader" method.
									     *
									    bool keepBond = intent.GetBooleanExtra(ExtraKeepBond, false);
									    if (keepBond && (fileType & TypeSoftDevice) == 0) 
                                        {
										    sendLogBroadcast(LogLevelVerbose, "Restarting service...");

										    updateProgressNotification(ProgressDisconnecting);
										    sendLogBroadcast(LogLevelVerbose, "Disconnecting...");
										    gatt.Disconnect();
										    waitUntilDisconnected();
										    sendLogBroadcast(LogLevelInfo, "Disconnected");

										    // Close the device
										    close(gatt);

										    Log.Info("DfuBaseService", "Restarting service");
										    Intent newIntent = new Intent();
										    newIntent.FillIn(intent, Intent.FILL_IN_COMPONENT | Intent.FILL_IN_PACKAGE);
										    startService(newIntent);
										    return;
									    }
								    } 
                                    else 
                                    {
									    sendLogBroadcast(LogLevelApplication, "Service Changed indications enabled");
								    }
								    hasServiceChanged = true;
							    }
						    }
					    }

					    sendLogBroadcast(LogLevelVerbose, "Jumping to the DFU Bootloader...");

					    // Enable notifications
					    enableCCCD(gatt, controlPointCharacteristic, NOTIFICATIONS);
					    sendLogBroadcast(LogLevelApplication, "Notifications enabled");

					    // Send 'jump to bootloader command' (Start DFU)
					    updateProgressNotification(ProgressEnablingDfuMode);
					    _opCodeStartDfu[1] = 0x04;
					    Log.Info("DfuBaseService", "Sending Start DFU command (Op Code = 1, Upload Mode = 4)");
					    writeOpCode(gatt, controlPointCharacteristic, _opCodeStartDfu, true);
					    sendLogBroadcast(LogLevelApplication, "Jump to bootloader sent (Op Code = 1, Upload Mode = 4)");

					    // The device will reset so we don't have to send Disconnect signal.
					    waitUntilDisconnected();
					    sendLogBroadcast(LogLevelInfo, "Disconnected by the remote device");

					    /*
					     * We would like to avoid using the hack with refreshing the device (refresh method is not in the public API). The refresh method clears the cached services and causes a
					     * service discovery afterwards (when connected). Android, however, does it itself when receive the Service Changed indication when bonded.
					     * In case of unpaired device we may either refresh the services manually (using the hack), or include the Service Changed characteristic.
					     *
					     * According to Bluetooth Core 4.0 (and 4.1) specification:
					     *
					     * [Vol. 3, Part G, 2.5.2 - Attribute Caching]
					     * Note: Clients without a trusted relationship must perform service discovery on each connection if the server supports the Services Changed characteristic.
					     *
					     * However, as up to Android 5 the system does NOT respect this requirement and servers are cached for every device, even if Service Changed is enabled -> Android BUG?
					     * For bonded devices Android performs service re-discovery when SC indication is received.
					     *
					    refreshDeviceCache(gatt, !hasServiceChanged);

					    // Close the device
					    close(gatt);

					    Log.Info("DfuBaseService", "Starting service that will connect to the DFU bootloader");
					    Intent newIntent = new Intent();
					    newIntent.fillIn(intent, Intent.FILL_IN_COMPONENT | Intent.FILL_IN_PACKAGE);
					    startService(newIntent);
					    return;
				    }

				    // Enable notifications
				    enableCCCD(gatt, controlPointCharacteristic, NOTIFICATIONS);
				    sendLogBroadcast(LogLevelApplication, "Notifications enabled");

				    try 
                    {
					    // Set up the temporary variable that will hold the responses
					    byte[] response;
					    int status;

					    /*
					     * The first version of DFU supported only an Application update.
					     * Initializing procedure:
					     * [DFU Start (0x01)] -> DFU Control Point
					     * [App size in bytes (UINT32)] -> DFU Packet
					     * ---------------------------------------------------------------------
					     * Since SDK 6.0 and Soft Device 7.0+ the DFU supports upgrading Soft Device, Bootloader and Application.
					     * Initializing procedure:
					     * [DFU Start (0x01), <Update Mode>] -> DFU Control Point
					     * [SD size in bytes (UINT32), Bootloader size in bytes (UINT32), Application size in bytes (UINT32)] -> DFU Packet
					     * where <Upload Mode> is a bit mask:
					     * 0x01 - Soft Device update
					     * 0x02 - Bootloader update
					     * 0x04 - Application update
					     * so that
					     * 0x03 - Soft Device and Bootloader update
					     * If <Upload Mode> equals 5, 6 or 7 DFU target may return OPERATION_NOT_SUPPORTED [10, 01, 03]. In that case service will try to send
					     * Soft Device and/or Bootloader first, reconnect to the new Bootloader and send the Application in the second connection.
					     * --------------------------------------------------------------------
					     * If DFU target supports only the old DFU, a response [10, 01, 03] will be send as a notification on DFU Control Point characteristic, where:
					     * 10 - Response for...
					     * 01 - DFU Start command
					     * 03 - Operation Not Supported
					     * (see table below)
					     * In that case:
					     * 1. If this is application update - service will try to upload using the old DFU protocol.
					     * 2. In case of SD or BL update an error is returned.
					     *

					    // Obtain size of image(s)
					    int softDeviceImageSize = (fileType & TypeSoftDevice) > 0 ? imageSizeInBytes : 0;
					    int bootloaderImageSize = (fileType & TypeBootloader) > 0 ? imageSizeInBytes : 0;
					    int appImageSize = (fileType & TypeApplication) > 0 ? imageSizeInBytes : 0;
					    // The sizes above may be overwritten if a ZIP file was passed
					    if (MimeTypeZip.Equals(mimeType)) 
                        {
						    ArchiveInputStream zhis = (ArchiveInputStream) is;
						    softDeviceImageSize = zhis.softDeviceImageSize();
						    bootloaderImageSize = zhis.bootloaderImageSize();
						    appImageSize = zhis.applicationImageSize();
					    }

					    try {
						    _opCodeStartDfu[1] = (byte) fileType;

						    // Send Start DFU command to Control Point
						    Log.Info("DfuBaseService", "Sending Start DFU command (Op Code = 1, Upload Mode = " + fileType + ")");
						    writeOpCode(gatt, controlPointCharacteristic, _opCodeStartDfu);
						    sendLogBroadcast(LogLevelApplication, "DFU Start sent (Op Code = 1, Upload Mode = " + fileType + ")");

						    // Send image size in bytes to DFU Packet
						    Log.Info("DfuBaseService", "Sending image size array to DFU Packet (" + softDeviceImageSize + "b, " + bootloaderImageSize + "b, " + appImageSize + "b)");
						    writeImageSize(gatt, packetCharacteristic, softDeviceImageSize, bootloaderImageSize, appImageSize);
						    sendLogBroadcast(LogLevelApplication, "Firmware image size sent (" + softDeviceImageSize + "b, " + bootloaderImageSize + "b, " + appImageSize + "b)");

						    // A notification will come with confirmation. Let's wait for it a bit
						    response = readNotificationResponse();

						    /*
						     * The response received from the DFU device contains:
						     * +---------+--------+----------------------------------------------------+
						     * | byte no | value  | description                                        |
						     * +---------+--------+----------------------------------------------------+
						     * | 0       | 16     | Response code                                      |
						     * | 1       | 1      | The Op Code of a request that this response is for |
						     * | 2       | STATUS | See DFU_STATUS_* for status codes                  |
						     * +---------+--------+----------------------------------------------------+
						     *
						    status = getStatusCode(response, _opCodeStartDfuKey);
						    sendLogBroadcast(LogLevelApplication, "Response received (Op Code = " + response[1] + " Status = " + status + ")");
						    if (status != DfuStatusSuccess)
							    throw new RemoteDfuException("Starting DFU failed", status);
					    } 
                        catch (RemoteDfuException e) 
                        {
						    try 
                            {
							    if (e.ErrorNumber != DfuStatusNotSupported)
								    throw new RemoteDfuException(e.Message, e.ErrorNumber);
							    // If user wants to send the Soft Device and/or the Bootloader + Application we may try to send the Soft Device/Bootloader files first,
							    // and then reconnect and send the application in the second connection.
							    if ((fileType & TypeApplication) > 0 && (fileType & (TypeSoftDevice | TypeBootloader)) > 0) 
                                {
                                    /*
								    // Clear the remote error flag
								    _remoteErrorOccurred = false;

								    Log.Warn("DfuBaseService", "DFU target does not support (SD/BL)+App update");
								    sendLogBroadcast(LogLevelWarning, "DFU target does not support (SD/BL)+App update");

								    fileType &= ~TypeApplication; // clear application bit
								    _fileType = fileType;
								    _opCodeStartDfu[1] = (byte) fileType;
								    _partsTotal = 2;

								    // Set new content type in the ZIP Input Stream and update sizes of images
								    ArchiveInputStream zhis = (ArchiveInputStream) is;
								    zhis.setContentType(fileType);
								    try 
                                    {
									    appImageSize = 0;
									    _imageSizeInBytes = is.available();
								    }
                                    catch (IOException e1) 
                                    {
									    // never happen
								    }

								    // Send Start DFU command to Control Point
								    sendLogBroadcast(LogLevelVerbose, "Sending only SD/BL");
								    Log.Info("DfuBaseService", "Resending Start DFU command (Op Code = 1, Upload Mode = " + fileType + ")");
								    writeOpCode(gatt, controlPointCharacteristic, _opCodeStartDfu);
								    sendLogBroadcast(LogLevelApplication, "DFU Start sent (Op Code = 1, Upload Mode = " + fileType + ")");

								    // Send image size in bytes to DFU Packet
								    Log.Info("DfuBaseService", "Sending image size array to DFU Packet: [" + softDeviceImageSize + "b, " + bootloaderImageSize + "b, " + appImageSize + "b]");
								    writeImageSize(gatt, packetCharacteristic, softDeviceImageSize, bootloaderImageSize, appImageSize);
								    sendLogBroadcast(LogLevelApplication, "Firmware image size sent [" + softDeviceImageSize + "b, " + bootloaderImageSize + "b, " + appImageSize + "b]");

								    // A notification will come with confirmation. Let's wait for it a bit
								    response = readNotificationResponse();
								    status = getStatusCode(response, _opCodeStartDfuKey);
								    sendLogBroadcast(LogLevelApplication, "Response received (Op Code = " + response[1] + " Status = " + status + ")");
								    if (status != DfuStatusSuccess)
									    throw new RemoteDfuException("Starting DFU failed", status);
							    } 
                                //else
								//    throw e;
						    } 
                            catch (RemoteDfuException e1) 
                            {
							    if (e1.ErrorNumber != DfuStatusNotSupported)
								    throw new RemoteDfuException(e1.Message, e1.ErrorNumber);

							    // If operation is not supported by DFU target we may try to upload application with legacy mode, using the old DFU protocol
							    if (fileType == TypeApplication) 
                                {
								    // Clear the remote error flag
								    _remoteErrorOccurred = false;

								    // The DFU target does not support DFU v.2 protocol
								    Log.Warn("DfuBaseService", "DFU target does not support DFU v.2");
								    sendLogBroadcast(LogLevelWarning, "DFU target does not support DFU v.2");

								    // Send Start DFU command to Control Point
								    sendLogBroadcast(LogLevelVerbose, "Switching to DFU v.1");
								    Log.Info("DfuBaseService", "Resending Start DFU command (Op Code = 1)");
								    writeOpCode(gatt, controlPointCharacteristic, _opCodeStartDfu); // If has 2 bytes, but the second one is ignored
								    sendLogBroadcast(LogLevelApplication, "DFU Start sent (Op Code = 1)");

								    // Send image size in bytes to DFU Packet
								    Log.Info("DfuBaseService", "Sending application image size to DFU Packet: " + imageSizeInBytes + " bytes");
								    writeImageSize(gatt, packetCharacteristic, _imageSizeInBytes);
								    sendLogBroadcast(LogLevelApplication, "Firmware image size sent (" + imageSizeInBytes + " bytes)");

								    // A notification will come with confirmation. Let's wait for it a bit
								    response = readNotificationResponse();
								    status = getStatusCode(response, _opCodeStartDfuKey);
								    sendLogBroadcast(LogLevelApplication, "Response received (Op Code = " + response[1] + ", Status = " + status + ")");
								    if (status != DfuStatusSuccess)
									    throw new RemoteDfuException("Starting DFU failed", status);
							    } 
                                else
                                {
								    throw new RemoteDfuException(e1.Message, e1.ErrorNumber);
                                }
						    }
					    }

					    // Since SDK 6.1 this delay is no longer required as the Receive Start DFU notification is postponed until the memory is clear.

					    //		if ((fileType & TypeSoftDevice) > 0) {
					    //			// In the experimental version of bootloader (SDK 6.0.0) we must wait some time until we can proceed with Soft Device update. Bootloader must prepare the RAM for the new firmware.
					    //			// Most likely this step will not be needed in the future as the notification received a moment before will be postponed until Bootloader is ready.
					    //			synchronized (this) {
					    //				try {
					    //					wait(6000);
					    //				} catch (InterruptedException e) {
					    //					// do nothing
					    //				}
					    //			}
					    //		}

					    /*
					     * If the DFU Version characteristic is present and the version returned from it is greater or equal to 0.5, the Extended Init Packet is required.
					     * For older versions, or if the DFU Version characteristic is not present (pre SDK 7.0.0), the Init Packet (which could have contained only the firmware CRC) was optional.
					     * To calculate the CRC (CRC-CCTII-16 0xFFFF) the following application may be used: http://www.lammertbies.nl/comm/software/index.html -> CRC library.
					     *
					     * The Init Packet is read from the *.dat file as a binary file. This service you allows to specify the init packet file in two ways.
					     * Since SDK 8.0 and the DFU Library v0.6 using the Distribution packet (ZIP) is recommended. The distribution packet can be created using the
					     * *nrf utility* tool, available together with Master Control Panel v 3.8.0. See the DFU documentation at http://developer.nordicsemi.com for more details.
					     * An init file may be also provided as a separate file using the {@link #EXTRA_INIT_FILE_PATH} or {@link #EXTRA_INIT_FILE_URI} or in the ZIP file
					     * with the deprecated fixed naming convention:
					     *
					     *    a) If the ZIP file contain a softdevice.hex (or .bin) and/or bootloader.hex (or .bin) the 'system.dat' must also be included.
					     *       In case when both files are present the CRC should be calculated from the two BIN contents merged together.
					     *       This means: if there are softdevice.hex and bootloader.hex files in the ZIP file you have to convert them to BIN
					     *       (e.g. using: http://hex2bin.sourceforge.net/ application), copy them into a single file where the soft device is placed as the first one and calculate
					     *       the CRC for the whole file.
					     *
					     *    b) If the ZIP file contains a application.hex (or .bin) file the 'application.dat' file must be included and contain the Init packet for the application.
					     *
					    // Send DFU Init Packet
					    if (initIs != null) 
                        {
						    sendLogBroadcast(LogLevelApplication, "Writing Initialize DFU Parameters...");

						    Log.Info("DfuBaseService", "Sending the Initialize DFU Parameters START (Op Code = 2, Value = 0)");
						    writeOpCode(gatt, controlPointCharacteristic, _opCodeInitDfuParamsStart);

						    try {
							    byte[] data = new byte[20];
							    int size;
							    while ((size = initIs.read(data, 0, data.Length)) != -1) {
								    writeInitPacket(gatt, packetCharacteristic, data, size);
							    }
						    } catch (IOException e) {
							    Log.Error("DfuBaseService", "Error while reading Init packet file");
							    throw new DfuException("Error while reading Init packet file", ErrorFileError);
						    }
						    Log.Info("DfuBaseService", "Sending the Initialize DFU Parameters COMPLETE (Op Code = 2, Value = 1)");
						    writeOpCode(gatt, controlPointCharacteristic, _opCodeInitDfuParamsComplete);
						    sendLogBroadcast(LogLevelApplication, "Initialize DFU Parameters completed");

						    // A notification will come with confirmation. Let's wait for it a bit
						    response = readNotificationResponse();
						    status = getStatusCode(response, _opCodeInitDfuParamsKey);
						    sendLogBroadcast(LogLevelApplication, "Response received (Op Code = " + response[1] + ", Status = " + status + ")");
						    if (status != DfuStatusSuccess)
							    throw new RemoteDfuException("Device returned error after sending init packet", status);
					    } 
                        else
                        {
						    _initPacketSent = true;
                        }

					    // Send the number of packets of firmware before receiving a receipt notification
					    int numberOfPacketsBeforeNotification = _packetsBeforeNotification;
					    if (numberOfPacketsBeforeNotification > 0) {
						    Log.Info("DfuBaseService", "Sending the number of packets before notifications (Op Code = 8, Value = " + numberOfPacketsBeforeNotification + ")");
						    setNumberOfPackets(_opCodePacketReceiptNotIfReq, numberOfPacketsBeforeNotification);
						    writeOpCode(gatt, controlPointCharacteristic, _opCodePacketReceiptNotIfReq);
						    sendLogBroadcast(LogLevelApplication, "Packet Receipt Notif Req (Op Code = 8) sent (Value = " + numberOfPacketsBeforeNotification + ")");
					    }

					    // Initialize firmware upload
					    Log.Info("DfuBaseService", "Sending Receive Firmware Image request (Op Code = 3)");
					    writeOpCode(gatt, controlPointCharacteristic, _opCodeReceiveFirmwareImage);
					    sendLogBroadcast(LogLevelApplication, "Receive Firmware Image request sent");

					    // Send the firmware. The method below sends the first packet and waits until the whole firmware is sent.
					    long startTime = _lastProgressTime = mStartTime = SystemClock.ElapsedRealtime();
					    updateProgressNotification();
					    try {
						    Log.Info("DfuBaseService", "Uploading firmware...");
						    sendLogBroadcast(LogLevelApplication, "Uploading firmware...");
						    response = uploadFirmwareImage(gatt, packetCharacteristic, is);
					    } catch (DeviceDisconnectedException e) {
						    Log.Error("DfuBaseService", "Disconnected while sending data");
						    throw e;
						    // TODO reconnect?
					    }
					    long endTime = SystemClock.elapsedRealtime();

					    // Check the result of the operation
					    status = getStatusCode(response, _opCodeReceiveFirmwareImageKey);
					    Log.Info("DfuBaseService", "Response received. Op Code: " + response[0] + " Req Op Code = " + response[1] + ", Status = " + response[2]);
					    sendLogBroadcast(LogLevelApplication, "Response received (Op Code = " + response[1] + ", Status = " + status + ")");
					    if (status != DfuStatusSuccess)
						    throw new RemoteDfuException("Device returned error after sending file", status);

					    Log.Info("DfuBaseService", "Transfer of " + _bytesSent + " bytes has taken " + (endTime - startTime) + " ms");
					    sendLogBroadcast(LogLevelApplication, "Upload completed in " + (endTime - startTime) + " ms");

					    // Send Validate request
					    Log.Info("DfuBaseService", "Sending Validate request (Op Code = 4)");
					    writeOpCode(gatt, controlPointCharacteristic, _OpCodeValidate);
					    sendLogBroadcast(LogLevelApplication, "Validate request sent");

					    // A notification will come with status code. Let's wait for it a bit.
					    response = readNotificationResponse();
					    status = getStatusCode(response, _opCodeValidateKey);
					    Log.Info("DfuBaseService", "Response received. Op Code: " + response[0] + " Req Op Code = " + response[1] + ", Status = " + response[2]);
					    sendLogBroadcast(LogLevelApplication, "Response received (Op Code = " + response[1] + ", Status = " + status + ")");
					    if (status != DfuStatusSuccess)
						    throw new RemoteDfuException("Device returned validation error", status);

					    // Send Activate and Reset signal.
					    updateProgressNotification(ProgressDisconnecting);
					    Log.Info("DfuBaseService", "Sending Activate and Reset request (Op Code = 5)");
					    writeOpCode(gatt, controlPointCharacteristic, _opCodeActivateAndReset);
					    sendLogBroadcast(LogLevelApplication, "Activate and Reset request sent");

					    // The device will reset so we don't have to send Disconnect signal.
					    waitUntilDisconnected();
					    sendLogBroadcast(LogLevelInfo, "Disconnected by the remote device");

					    // In the DFU version 0.5, in case the device is bonded, the target device does not send the Service Changed indication after
					    // a jump from bootloader mode to app mode. This issue has been fixed in DFU version 0.6 (SDK 8.0). If the DFU bootloader has been
					    // configured to preserve the bond information we do not need to enforce refreshing services, as it will notify the phone using the
					    // Service Changed indication.
					    bool keepBond = intent.getBooleanExtra(ExtraKeepBond, false);
					    refreshDeviceCache(gatt, version == 5 || !keepBond);

					    // Close the device
					    close(gatt);

					    // During the update the bonding information on the target device may have been removed.
					    // To create bond with the new application set the ExtraRestoreBond extra to true.
					    // In case the bond information is copied to the new application the new bonding is not required.
					    if (gatt.Device.BondState == Bond.Bonded) 
                        {
						    bool restoreBond = intent.GetBooleanExtra(ExtraRestoreBond, false);

						    if (restoreBond || !keepBond || (fileType & TypeSoftDevice) > 0) {
							    // The bond information was lost.
							    removeBond(gatt.Device);

							    // Give some time for removing the bond information. 300ms was too short, let's set it to 2 seconds just to be sure.
                                await Task.Delay(2000);
						    }

						    if (restoreBond && (fileType & TypeApplication) > 0) 
                            {
							    // Restore pairing when application was updated.
							    createBond(gatt.getDevice());
						    }
					    }

					    /*
					     * We need to send ProgressCompleted message only when all files has been transmitted.
					     * In case you want to send the Soft Device and/or Bootloader and the Application, the service will be started twice: one to send SD+BL, and the
					     * second time to send the Application only (using the new Bootloader). In the first case we do not send ProgressCompleted notification.
					     *
					    if (_partCurrent == _partsTotal) 
                        {
						    // Delay this event a little bit. Android needs some time to prepare for reconnection.
                            await Task.Delay(1400);
						    updateProgressNotification(ProgressCompleted);
					    } 
                        else 
                        {
						    /*
						     * The current service handle will try to upload Soft Device and/or Bootloader.
						     * We need to enqueue another Intent that will try to send application only.
						     *
						    Log.Info("DfuBaseService", "Starting service that will upload application");
						    Intent newIntent = new Intent();
						    newIntent.fillIn(intent, Intent.FILL_IN_COMPONENT | Intent.FILL_IN_PACKAGE);
						    newIntent.putExtra(EXTRA_FILE_MIME_TYPE, MIME_TYPE_ZIP); // ensure this is set (f.e. for scripts)
						    newIntent.putExtra(EXTRA_FILE_TYPE, TypeApplication); // set the type to application only
						    newIntent.putExtra(EXTRA_PART_CURRENT, _partCurrent + 1);
						    newIntent.putExtra(EXTRA_PARTS_TOTAL, _partsTotal);
						    startService(newIntent);
					    }
				    } 
                    catch (UnknownResponseException e) 
                    {
					    int error = ERROR_INVALID_RESPONSE;
					    Log.Error("DfuBaseService", e.getMessage());
					    sendLogBroadcast(LogLevelError, e.getMessage());

					    Log.Info("DfuBaseService", "Sending Reset command (Op Code = 6)");
					    writeOpCode(gatt, controlPointCharacteristic, OP_CODE_RESET);
					    sendLogBroadcast(LogLevelApplication, "Reset request sent");
					    terminateConnection(gatt, error);
				    }
                    catch (RemoteDfuException e) 
                    {
					    int error = ERROR_REMOTE_MASK | e.ErrorNumber;
					    Log.Error("DfuBaseService", e.getMessage());
					    sendLogBroadcast(LogLevelError, String.format("Remote DFU error: %s", GattError.parse(error)));

					    Log.Info("DfuBaseService", "Sending Reset command (Op Code = 6)");
					    writeOpCode(gatt, controlPointCharacteristic, OP_CODE_RESET);
					    sendLogBroadcast(LogLevelApplication, "Reset request sent");
					    terminateConnection(gatt, error);
				    }
			    } 
                catch (UploadAbortedException e) 
                {
				    Log.Info("DfuBaseService", "Upload aborted");
				    sendLogBroadcast(LogLevelWarning, "Upload aborted");
				    if (_connectionState == StateConnectedAndReady)
                    {
					    try 
                        {
						    _aborted = false;
						    Log.Info("DfuBaseService", "Sending Reset command (Op Code = 6)");
						    writeOpCode(gatt, controlPointCharacteristic, OP_CODE_RESET);
						    sendLogBroadcast(LogLevelApplication, "Reset request sent");
					    } 
                        catch (Exception e1) 
                        {
						    // do nothing
					    }
                    }
				    terminateConnection(gatt, ProgressAborted);
			    } 
                catch (DeviceDisconnectedException e) 
                {
				    sendLogBroadcast(LogLevelError, "Device has disconnected");
				    // TODO reconnect n times?
				    Log.Error("DfuBaseService", e.getMessage());
				    close(gatt);
				    updateProgressNotification(ErrorDeviceDisconnected);
			    } 
                catch (DfuException e) 
                {
				    int error = e.ErrorNumber;
				    // Connection state errors and other Bluetooth GATT callbacks share the same error numbers. Therefore we are using bit masks to identify the type.
				    if ((error & ErrorConnectionStateMask) > 0) {
					    error &= ~ErrorConnectionStateMask;
					    sendLogBroadcast(LogLevelError, String.format("Error (0x%02X): %s", error, GattError.parseConnectionError(error)));
				    } else {
					    error &= ~ErrorConnectionMask;
					    sendLogBroadcast(LogLevelError, String.format("Error (0x%02X): %s", error, GattError.parse(error)));
				    }
				    Log.Error("DfuBaseService", e.getMessage());
				    if (_connectionState == StateConnectedAndReady)
					    try {
						    Log.Info("DfuBaseService", "Sending Reset command (Op Code = 6)");
						    writeOpCode(gatt, controlPointCharacteristic, _opCodeReset);
						    sendLogBroadcast(LogLevelApplication, "Reset request sent");
					    } catch (Exception e1) {
						    // do nothing
					    }
				    terminateConnection(gatt, e.ErrorNumber); // we return the whole error number, including the error type mask
			    }
		    } 
            finally    
            {
			    try {
				    // Ensure that input stream is always closed
				    mInputStream = null;
				    if (is != null)
					    is.close();
			    } catch (IOException e) {
				    // do nothing
			    }
		    }
            */ 
	    }

	    /**
	     * Sets number of data packets that will be send before the notification will be received.
	     *
	     * @param data  control point data packet
	     * @param value number of packets before receiving notification. If this value is 0, then the notification of packet receipt will be disabled by the DFU target.
	     */
	    private void setNumberOfPackets(byte[] data, int value) 
        {
		    data[1] = (byte) (value & 0xFF);
		    data[2] = (byte) ((value >> 8) & 0xFF);
	    }

	    /**
	     * Opens the binary input stream that returns the firmware image content. A Path to the file is given.
	     *
	     * @param filePath the path to the HEX or BIN file
	     * @param mimeType the file type
	     * @param mbrSize  the size of MBR, by default 0x1000
	     * @param types    the content files types in ZIP
	     * @return the input stream with binary image content
	     */
	    private InputStream openInputStream(String filePath, String mimeType, int mbrSize, int types) 
        {
		    InputStream is = new FileInputStream(filePath);
		    if (MimeTypeZip.Equals(mimeType))
			    return new ArchiveInputStream(is, mbrSize, types);
		    if (filePath.ToLower().EndsWith("hex"))
			    return new HexInputStream(is, mbrSize);
		    return is;
	    }

	    /**
	     * Opens the binary input stream. A Uri to the stream is given.
	     *
	     * @param stream   the Uri to the stream
	     * @param mimeType the file type
	     * @param mbrSize  the size of MBR, by default 0x1000
	     * @param types    the content files types in ZIP
	     * @return the input stream with binary image content
	     *
	    private InputStream openInputStream(Uri stream, String mimeType, int mbrSize, int types) 
        {
		    InputStream is = getContentResolver().openInputStream(stream);
		    if (MimeTypeZip.equals(mimeType))
			    return new ArchiveInputStream(is, mbrSize, types);

		    String[] projection = {MediaStore.Images.Media.DISPLAY_NAME};
		    Cursor cursor = getContentResolver().query(stream, projection, null, null, null);
		    try {
			    if (cursor.moveToNext()) {
				    String fileName = cursor.getString(0); // DISPLAY_NAME);

				    if (fileName.ToLower().EndsWith("hex"))
					    return new HexInputStream(is, mbrSize);
			    }
		    } finally {
			    cursor.close();
		    }
		    return is;
	    }*/

	    /**
	     * Connects to the BLE device with given address. This method is SYNCHRONOUS, it wait until the connection status change from {@link #StateConnecting} to {@link #StateConnectedAndReady} or an
	     * error occurs. This method returns <code>null</code> if Bluetooth adapter is disabled.
	     *
	     * @param address the device address
	     * @return the GATT device or <code>null</code> if Bluetooth adapter is disabled.
	     */
	    private BluetoothGatt connect(String address) 
        {
		    if (!_bluetoothAdapter.IsEnabled)
			    return null;

		    _connectionState = StateConnecting;

		    Log.Info("DfuBaseService", "Connecting to the device...");
		    BluetoothDevice device = _bluetoothAdapter.GetRemoteDevice(address);
		    BluetoothGatt gatt = device.ConnectGatt(this, false, _gattCallback);

		    // We have to wait until the device is connected and services are discovered
		    // Connection error may occur as well.
		    try 
            {
			    synchronized (_lock) 
                {
				    while (((_connectionState == StateConnecting || _connectionState == StateConnected) && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } 
            catch (InterruptedException e) 
            {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    return gatt;
	    }

	    /**
	     * Disconnects from the device and cleans local variables in case of error. This method is SYNCHRONOUS and wait until the disconnecting process will be completed.
	     *
	     * @param gatt  the GATT device to be disconnected
	     * @param error error number
	     */
	    private void terminateConnection(BluetoothGatt gatt, int error) 
        {
		    if (_connectionState != StateDisconnected) {
			    updateProgressNotification(ProgressDisconnecting);

			    // No need to disable notifications

			    // Disconnect from the device
			    disconnect(gatt);
			    sendLogBroadcast(LogLevelInfo, "Disconnected");
		    }

		    // Close the device
		    refreshDeviceCache(gatt, false); // This should be set to true when DFU Version is 0.5 or lower
		    close(gatt);
		    updateProgressNotification(error);
	    }

	    /**
	     * Disconnects from the device. This is SYNCHRONOUS method and waits until the callback returns new state. Terminates immediately if device is already disconnected. Do not call this method
	     * directly, use {@link #terminateConnection(android.bluetooth.BluetoothGatt, int)} instead.
	     *
	     * @param gatt the GATT device that has to be disconnected
	     */
	    private void disconnect(BluetoothGatt gatt) 
        {
		    if (_connectionState == StateDisconnected)
			    return;

		    _connectionState = StateDisconnecting;

		    Log.Info("DfuBaseService", "Disconnecting from the device...");
		    gatt.Disconnect();

		    // We have to wait until device gets disconnected or an error occur
		    waitUntilDisconnected();
	    }

	    /**
	     * Wait until the connection state will change to {@link #StateDisconnected} or until an error occurs.
	     */
	    private void waitUntilDisconnected() 
        {
		    try {
			    synchronized (_lock) 
                {
				    while (_connectionState != StateDisconnected && _error == 0)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
	    }

	    /**
	     * Closes the GATT device and cleans up.
	     *
	     * @param gatt the GATT device to be closed
	     */
	    private void close(BluetoothGatt gatt) 
        {
		    Log.Info("DfuBaseService", "Cleaning up...");
		    sendLogBroadcast(LogLevelDebug, "gatt.close()");
		    gatt.Close();
		    _connectionState = StateClosed;
	    }

	    /**
	     * Clears the device cache. After uploading new firmware the DFU target will have other services than before.
	     *
	     * @param gatt  the GATT device to be refreshed
	     * @param force <code>true</code> to force the refresh
	     */
	    private void refreshDeviceCache(BluetoothGatt gatt, bool force) 
        {
		    /*
		     * If the device is bonded this is up to the Service Changed characteristic to notify Android that the services has changed.
		     * There is no need for this trick in that case.
		     * If not bonded, the Android should not keep the services cached when the Service Changed characteristic is present in the target device database.
		     * However, due to the Android bug (still exists in Android 5.0.1), it is keeping them anyway and the only way to clear services is by using this hidden refresh method.
		     */
		    if (force || gatt.Device.BondState == Bond.None) 
            {
			    sendLogBroadcast(LogLevelDebug, "gatt.refresh()");
			    /*
			     * There is a refresh() method in BluetoothGatt class but for now it's hidden. We will call it using reflections.
			     */
			    try 
                {
				    Method refresh = gatt.Class.GetMethod("refresh");
				    if (refresh != null) 
                    {
					    bool success = (bool) refresh.Invoke(gatt);
					    Log.Info("DfuBaseService", "Refreshing result: " + success);
				    }
			    } 
                catch (System.Exception ex) 
                {
				    Log.Error("DfuBaseService", "An exception occurred while refreshing device", ex);
				    sendLogBroadcast(LogLevelWarning, "Refreshing failed");
			    }
		    }
	    }

	    /**
	     * Checks whether the response received is valid and returns the status code.
	     *
	     * @param response the response received from the DFU device.
	     * @param request  the expected Op Code
	     * @return the status code
	     * @throws UnknownResponseException if response was not valid
	     */
	    private int getStatusCode(byte[] response, int request) 
        {
		    if (response == null || response.Length != 3 || response[0] != _opCodeResponseCodeKey || response[1] != request || response[2] < 1 || response[2] > 6)
			    throw new UnknownResponseException("Invalid response received", response, request);
		    return response[2];
	    }

	    /**
	     * Reads the DFU Version characteristic if such exists. Otherwise it returns 0.
	     *
	     * @param gatt           the GATT device
	     * @param characteristic the characteristic to read
	     * @return a version number or 0 if not present on the bootloader
	     * @throws DeviceDisconnectedException
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private int readVersion(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic) 
        {
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to read version number", _connectionState);
		    // If the DFU Version characteristic is not available we return 0.
		    if (characteristic == null)
			    return 0;

		    _receivedData = null;
		    _error = 0;

		    Log.Info("DfuBaseService", "Reading DFU version number...");
		    sendLogBroadcast(LogLevelVerbose, "Reading DFU version number...");

		    gatt.ReadCharacteristic(characteristic);

		    // We have to wait until device receives a response or an error occur
		    try 
            {
			    synchronized (_lock) 
                {
				    while ((!_requestCompleted && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (_error != 0)
			    throw new DfuException("Unable to read version number", _error);
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to read version number", _connectionState);

		    // The version is a 16-bit unsigned int
		    return (int)characteristic.GetIntValue(GattFormat.Uint16, 0);
	    }

	    /**
	     * Enables or disables the notifications for given characteristic. This method is SYNCHRONOUS and wait until the
	     * {@link android.bluetooth.BluetoothGattCallback#onDescriptorWrite(android.bluetooth.BluetoothGatt, android.bluetooth.BluetoothGattDescriptor, int)} will be called or the connection state will change from {@link #StateConnectedAndReady}. If
	     * connection state will change, or an error will occur, an exception will be thrown.
	     *
	     * @param gatt           the GATT device
	     * @param characteristic the characteristic to enable or disable notifications for
	     * @param type           {@link #NOTIFICATIONS} or {@link #INDICATIONS}
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private void enableCCCD(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, int type) 
        {
		    String debugString = type == NOTIFICATIONS ? "notifications" : "indications";
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to set " + debugString + " state", _connectionState);

		    _receivedData = null;
		    _error = 0;
		    if ((type == NOTIFICATIONS && _notificationsEnabled) || (type == INDICATIONS && mServiceChangedIndicationsEnabled))
			    return;

		    Log.Info("DfuBaseService", "Enabling " + debugString + "...");
		    sendLogBroadcast(LogLevelVerbose, "Enabling " + debugString + " for " + characteristic.Uuid);

		    // enable notifications locally
		    gatt.SetCharacteristicNotification(characteristic, true);

		    // enable notifications on the device
		    BluetoothGattDescriptor descriptor = characteristic.GetDescriptor(CLIENT_CHARACTERISTIC_CONFIG);
		    descriptor.SetValue(type == NOTIFICATIONS ? BluetoothGattDescriptor.ENABLE_NOTIFICATION_VALUE : BluetoothGattDescriptor.ENABLE_INDICATION_VALUE);
		    sendLogBroadcast(LogLevelDebug, "gatt.writeDescriptor(" + descriptor.Uuid + (type == NOTIFICATIONS ? ", value=0x01-00)" : ", value=0x02-00)"));
		    gatt.WriteDescriptor(descriptor);

		    // We have to wait until device receives a response or an error occur
		    try 
            {
			    synchronized (_lock) 
                {
				    while ((((type == NOTIFICATIONS && !_notificationsEnabled) || (type == INDICATIONS && !_serviceChangedIndicationsEnabled))
						    && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } 
            catch (InterruptedException e) 
            {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (_error != 0)
			    throw new DfuException("Unable to set " + debugString + " state", _error);
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to set " + debugString + " state", _connectionState);
	    }

	    /**
	     * Reads the value of the Service Changed Client Characteristic Configuration descriptor (CCCD).
	     *
	     * @param gatt           the GATT device
	     * @param characteristic the Service Changed characteristic
	     * @return <code>true</code> if Service Changed CCCD is enabled ans set to INDICATE
	     * @throws DeviceDisconnectedException
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private bool isServiceChangedCCCDEnabled(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic) 
        {
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to read Service Changed CCCD", _connectionState);
		    // If the Service Changed characteristic or the CCCD is not available we return false.
		    if (characteristic == null)
			    return false;

		    BluetoothGattDescriptor descriptor = characteristic.GetDescriptor(_clientCharacteristicConfig);
		    if (descriptor == null)
			    return false;

		    _requestCompleted = false;
		    _error = 0;

		    Log.Info("DfuBaseService", "Reading Service Changed CCCD value...");
		    sendLogBroadcast(LogLevelVerbose, "Reading Service Changed CCCD value...");

		    gatt.ReadDescriptor(descriptor);

		    // We have to wait until device receives a response or an error occur
		    try 
            {
			    synchronized (_lock) 
                {
				    while ((!_requestCompleted && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } 
            catch (InterruptedException e) 
            {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (_error != 0)
			    throw new DfuException("Unable to read Service Changed CCCD", _error);
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to read Service Changed CCCD", _connectionState);

		    return _serviceChangedIndicationsEnabled;
	    }

	    /**
	     * Writes the operation code to the characteristic. This method is SYNCHRONOUS and wait until the
	     * {@link android.bluetooth.BluetoothGattCallback#onCharacteristicWrite(android.bluetooth.BluetoothGatt, android.bluetooth.BluetoothGattCharacteristic, int)} will be called or the connection state will change from {@link #StateConnectedAndReady}.
	     * If connection state will change, or an error will occur, an exception will be thrown.
	     *
	     * @param gatt           the GATT device
	     * @param characteristic the characteristic to write to. Should be the DFU CONTROL POINT
	     * @param value          the value to write to the characteristic
	     * @throws DeviceDisconnectedException
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private void writeOpCode(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value) 
        {
		    bool reset = value[0] == _opCodeResetKey || value[0] == _opCodeActivateAndResetKey;
		    writeOpCode(gatt, characteristic, value, reset);
	    }

	    /**
	     * Writes the operation code to the characteristic. This method is SYNCHRONOUS and wait until the
	     * {@link android.bluetooth.BluetoothGattCallback#onCharacteristicWrite(android.bluetooth.BluetoothGatt, android.bluetooth.BluetoothGattCharacteristic, int)} will be called or the connection state will change from {@link #StateConnectedAndReady}.
	     * If connection state will change, or an error will occur, an exception will be thrown.
	     *
	     * @param gatt           the GATT device
	     * @param characteristic the characteristic to write to. Should be the DFU CONTROL POINT
	     * @param value          the value to write to the characteristic
	     * @param reset          whether the command trigger restarting the device
	     * @throws DeviceDisconnectedException
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private void writeOpCode(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] value, bool reset) 
        {
		    _receivedData = null;
		    _error = 0;
		    _requestCompleted = false;
		    /*
		     * Sending a command that will make the DFU target to reboot may cause an error 133 (0x85 - Gatt Error). If so, with this flag set, the error will not be shown to the user
		     * as the peripheral is disconnected anyway. See: _gattCallback#onCharacteristicWrite(...) method
		     */
		    _resetRequestSent = reset;

		    characteristic.SetValue(value);
		    sendLogBroadcast(LogLevelVerbose, "Writing to characteristic " + characteristic.Uuid);
		    sendLogBroadcast(LogLevelDebug, "gatt.WriteCharacteristic(" + characteristic.Uuid + ")");
		    gatt.WriteCharacteristic(characteristic);

		    // We have to wait for confirmation
		    try {
			    synchronized (_lock) {
				    while ((!_requestCompleted && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (!_resetRequestSent && _error != 0)
			    throw new DfuException("Unable to write Op Code " + value[0], _error);
		    if (!_resetRequestSent && _connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to write Op Code " + value[0], _connectionState);
	    }

	    /**
	     * Writes the image size to the characteristic. This method is SYNCHRONOUS and wait until the {@link android.bluetooth.BluetoothGattCallback#onCharacteristicWrite(android.bluetooth.BluetoothGatt, android.bluetooth.BluetoothGattCharacteristic, int)}
	     * will be called or the connection state will change from {@link #StateConnectedAndReady}. If connection state will change, or an error will occur, an exception will be thrown.
	     *
	     * @param gatt           the GATT device
	     * @param characteristic the characteristic to write to. Should be the DFU PACKET
	     * @param imageSize      the image size in bytes
	     * @throws DeviceDisconnectedException
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private void writeImageSize(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, int imageSize) 
        {
		    _receivedData = null;
		    _error = 0;
		    _imageSizeSent = false;

            characteristic.WriteType = GattWriteType.NoResponse;
		    characteristic.SetValue(new byte[4]);
		    characteristic.SetValue(imageSize, GattFormat.Uint32, 0);
		    sendLogBroadcast(LogLevelVerbose, "Writing to characteristic " + characteristic.Uuid);
		    sendLogBroadcast(LogLevelDebug, "gatt.WriteCharacteristic(" + characteristic.Uuid + ")");
		    gatt.WriteCharacteristic(characteristic);

		    // We have to wait for confirmation
		    try {
			    synchronized (_lock) {
				    while ((!_imageSizeSent && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (_error != 0)
			    throw new DfuException("Unable to write Image Size", _error);
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to write Image Size", _connectionState);
	    }

	    /**
	     * <p>
	     * Writes the Soft Device, Bootloader and Application image sizes to the characteristic. Soft Device and Bootloader update is supported since Soft Device s110 v7.0.0.
	     * Sizes of SD, BL and App are uploaded as 3x UINT32 even though some of them may be 0s. F.e. if only App is being updated the data will be <0x00000000, 0x00000000, [App size]>
	     * </p>
	     * <p>
	     * This method is SYNCHRONOUS and wait until the {@link android.bluetooth.BluetoothGattCallback#onCharacteristicWrite(android.bluetooth.BluetoothGatt, android.bluetooth.BluetoothGattCharacteristic, int)} will be called or the connection state will
	     * change from {@link #StateConnectedAndReady}. If connection state will change, or an error will occur, an exception will be thrown.
	     * </p>
	     *
	     * @param gatt                the GATT device
	     * @param characteristic      the characteristic to write to. Should be the DFU PACKET
	     * @param softDeviceImageSize the Soft Device image size in bytes
	     * @param bootloaderImageSize the Bootloader image size in bytes
	     * @param appImageSize        the Application image size in bytes
	     * @throws DeviceDisconnectedException
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private void writeImageSize(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, int softDeviceImageSize, int bootloaderImageSize, int appImageSize)
        {
		    _receivedData = null;
		    _error = 0;
		    _imageSizeSent = false;

		    characteristic.WriteType = GattWriteType.NoResponse;
		    characteristic.SetValue(new byte[12]);
		    characteristic.SetValue(softDeviceImageSize, GattFormat.Uint32, 0);
		    characteristic.SetValue(bootloaderImageSize, GattFormat.Uint32, 4);
		    characteristic.SetValue(appImageSize, GattFormat.Uint32, 8);
		    sendLogBroadcast(LogLevelVerbose, "Writing to characteristic " + characteristic.Uuid);
		    sendLogBroadcast(LogLevelDebug, "gatt.WriteCharacteristic(" + characteristic.Uuid + ")");
		    gatt.WriteCharacteristic(characteristic);

		    // We have to wait for confirmation
		    try {
			    synchronized (_lock) {
				    while ((!_imageSizeSent && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (_error != 0)
			    throw new DfuException("Unable to write Image Sizes", _error);
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to write Image Sizes", _connectionState);
	    }

	    /**
	     * Writes the Init packet to the characteristic. This method is SYNCHRONOUS and wait until the {@link android.bluetooth.BluetoothGattCallback#onCharacteristicWrite(android.bluetooth.BluetoothGatt, android.bluetooth.BluetoothGattCharacteristic, int)}
	     * will be called or the connection state will change from {@link #StateConnectedAndReady}. If connection state will change, or an error will occur, an exception will be thrown.
	     *
	     * @param gatt           the GATT device
	     * @param characteristic the characteristic to write to. Should be the DFU PACKET
	     * @param buffer         the init packet as a byte array. This must be shorter or equal to 20 bytes (TODO check this restriction).
	     * @param size           the init packet size
	     * @throws DeviceDisconnectedException
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private void writeInitPacket(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] buffer, int size) 
        {
		    byte[] locBuffer = buffer;
		    if (buffer.Length != size) {
			    locBuffer = new byte[size];
			    System.arraycopy(buffer, 0, locBuffer, 0, size);
		    }
		    _receivedData = null;
		    _error = 0;
		    _initPacketSent = false;

		    characteristic.WriteType = GattWriteType.NoResponse;
		    characteristic.SetValue(locBuffer);
		    Log.Info("DfuBaseService", "Sending init packet (Value = " + parse(locBuffer) + ")");
		    sendLogBroadcast(LogLevelVerbose, "Writing to characteristic " + characteristic.Uuid);
		    sendLogBroadcast(LogLevelDebug, "gatt.WriteCharacteristic(" + characteristic.Uuid + ")");
		    gatt.WriteCharacteristic(characteristic);

		    // We have to wait for confirmation
		    try {
			    synchronized (_lock) {
				    while ((!_initPacketSent && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (_error != 0)
			    throw new DfuException("Unable to write Init DFU Parameters", _error);
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to write Init DFU Parameters", _connectionState);
	    }

	    /**
	     * Starts sending the data. This method is SYNCHRONOUS and terminates when the whole file will be uploaded or the connection status will change from {@link #StateConnectedAndReady}. If
	     * connection state will change, or an error will occur, an exception will be thrown.
	     *
	     * @param gatt                 the GATT device (DFU target)
	     * @param packetCharacteristic the characteristic to write file content to. Must be the DFU PACKET
	     * @return The response value received from notification with Op Code = 3 when all bytes will be uploaded successfully.
	     * @throws DeviceDisconnectedException Thrown when the device will disconnect in the middle of the transmission. The error core will be saved in {@link #_connectionState}.
	     * @throws DfuException                Thrown if DFU error occur
	     * @throws UploadAbortedException
	     */
	    private byte[] uploadFirmwareImage(BluetoothGatt gatt, BluetoothGattCharacteristic packetCharacteristic, InputStream inputStream) 
        {
		    _receivedData = null;
		    _error = 0;

		    byte[] buffer = _buffer;
		    try {
			    int size = inputStream.Read(buffer);
			    sendLogBroadcast(LogLevelVerbose, "Sending firmware to characteristic " + packetCharacteristic.Uuid + "...");
			    writePacket(gatt, packetCharacteristic, buffer, size);
		    } catch (HexFileValidationException e) {
			    throw new DfuException("HEX file not valid", ErrorFileInvalid);
		    } catch (IOException e) {
			    throw new DfuException("Error while reading file", ErrorFileIOException);
		    }

		    try {
			    synchronized (_lock) {
				    while ((_receivedData == null && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (_error != 0)
			    throw new DfuException("Uploading Firmware Image failed", _error);
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Uploading Firmware Image failed: device disconnected", _connectionState);

		    return _receivedData;
	    }

	    /**
	     * Writes the buffer to the characteristic. The maximum size of the buffer is 20 bytes. This method is ASYNCHRONOUS and returns immediately after adding the data to TX queue.
	     *
	     * @param gatt           the GATT device
	     * @param characteristic the characteristic to write to. Should be the DFU PACKET
	     * @param buffer         the buffer with 1-20 bytes
	     * @param size           the number of bytes from the buffer to send
	     */
	    private void writePacket(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, byte[] buffer, int size) 
        {
		    byte[] locBuffer = buffer;
		    if (buffer.Length != size) 
            {
			    locBuffer = new byte[size];
			    System.arraycopy(buffer, 0, locBuffer, 0, size);
		    }
		    characteristic.SetValue(locBuffer);
		    gatt.WriteCharacteristic(characteristic);
		    // FIXME BLE buffer overflow
		    // after writing to the device with WRITE_NO_RESPONSE property the onCharacteristicWrite callback is received immediately after writing data to a buffer.
		    // The real sending is much slower than adding to the buffer. This method does not return false if writing didn't succeed.. just the callback is not invoked.
		    //
		    // More info: this works fine on Nexus 5 (Android 4.4) (4.3 seconds) and on Samsung S4 (Android 4.3) (20 seconds) so this is a driver issue.
		    // Nexus 4 and 7 uses Qualcomm chip, Nexus 5 and Samsung uses Broadcom chips.
	    }

	    private void waitIfPaused() 
        {
		    synchronized (_lock) 
            {
			    try 
                {
				    while (_paused)
					    _lock.wait();
			    } 
                catch (InterruptedException e) 
                {
				    Log.Error("DfuBaseService", "Sleeping interrupted", e);
			    }
		    }
	    }

	    private bool createBond(BluetoothDevice device) 
        {
		    if (device.BondState == Bond.Bonded)
			    return true;

		    bool result;
		    _requestCompleted = false;

		    sendLogBroadcast(LogLevelVerbose, "Starting pairing...");
		    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT) {
			    sendLogBroadcast(LogLevelDebug, "gatt.getDevice().createBond()");
			    result = device.CreateBond();
		    } else {
			    result = createBondApi18(device);
		    }

		    // We have to wait until device is bounded
		    try {
			    synchronized (_lock) {
				    while (!_requestCompleted && !_aborted)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    return result;
	    }

	    private bool createBondApi18(BluetoothDevice device) 
        {
		    /*
		     * There is a createBond() method in BluetoothDevice class but for now it's hidden. We will call it using reflections. It has been revealed in KitKat (Api19)
		     */
		    try 
            {
			    Method createBond = device.Class.GetMethod("createBond");
			    if (createBond != null) 
                {
				    sendLogBroadcast(LogLevelDebug, "gatt.getDevice().createBond() (hidden)");
				    return (bool)createBond.Invoke(device);
			    }
		    } 
            catch (System.Exception ex) 
            {
			    Log.Warn("DfuBaseService", "An exception occurred while creating bond", ex);
		    }
		    return false;
	    }

	    /**
	     * Removes the bond information for the given device.
	     *
	     * @param device the device to unbound
	     * @return <code>true</code> if operation succeeded, <code>false</code> otherwise
	     */
	    private bool removeBond(BluetoothDevice device) 
        {
		    if (device.BondState == Bond.None)
			    return true;

		    sendLogBroadcast(LogLevelVerbose, "Removing bond information...");
		    bool result = false;
		    /*
		     * There is a removeBond() method in BluetoothDevice class but for now it's hidden. We will call it using reflections.
		     */
		    try {
			    Method removeBond = device.Class.GetMethod("removeBond");
			    if (removeBond != null) {
				    _requestCompleted = false;
				    sendLogBroadcast(LogLevelDebug, "gatt.getDevice().removeBond() (hidden)");
				    result = (bool) removeBond.Invoke(device);

				    // We have to wait until device is unbounded
				    try {
					    synchronized (_lock) {
						    while (!_requestCompleted && !_aborted)
							    _lock.wait();
					    }
				    } catch (InterruptedException ex) {
					    Log.Error("DfuBaseService", "Sleeping interrupted", ex);
				    }
			    }
			    result = true;
		    } catch (System.Exception ex) {
			    Log.Warn("DfuBaseService", "An exception occurred while removing bond information", ex);
		    }
		    return result;
	    }

	    /**
	     * Waits until the notification will arrive. Returns the data returned by the notification. This method will block the thread if response is not ready or connection state will change from
	     * {@link #StateConnectedAndReady}. If connection state will change, or an error will occur, an exception will be thrown.
	     *
	     * @return the value returned by the Control Point notification
	     * @throws DeviceDisconnectedException
	     * @throws DfuException
	     * @throws UploadAbortedException
	     */
	    private byte[] readNotificationResponse() 
        {
		    // do not clear the mReceiveData here. The response might already be obtained. Clear it in write request instead.
		    _error = 0;
		    try {
			    synchronized (_lock) {
				    while ((_receivedData == null && _connectionState == StateConnectedAndReady && _error == 0 && !_aborted) || _paused)
					    _lock.wait();
			    }
		    } catch (InterruptedException e) {
			    Log.Error("DfuBaseService", "Sleeping interrupted", e);
		    }
		    if (_aborted)
			    throw new UploadAbortedException();
		    if (_error != 0)
			    throw new DfuException("Unable to write Op Code", _error);
		    if (_connectionState != StateConnectedAndReady)
			    throw new DeviceDisconnectedException("Unable to write Op Code", _connectionState);
		    return _receivedData;
	    }

	    /**
	     * Creates or updates the notification in the Notification Manager. Sends broadcast with current progress to the activity.
	     */
	    private void updateProgressNotification() 
        {
		    int progress = (int) (100.0f * _bytesSent / _imageSizeInBytes);
		    if (_lastProgress == progress)
			    return;

            _lastProgress = progress;
		    updateProgressNotification(progress);
	    }

	    /**
	     * Creates or updates the notification in the Notification Manager. Sends broadcast with given progress or error state to the activity.
	     *
	     * @param progress the current progress state or an error number, can be one of {@link #ProgressConnecting}, {@link #PROGRESS_STARTING}, {@link #PROGRESS_ENABLING_DFU_MODE},
	     *                 {@link #PROGRESS_VALIDATING}, {@link #ProgressDisconnecting}, {@link #ProgressCompleted} or {@link #ErrorFileError}, {@link #ErrorFileInvalid} , etc
	     */
	    private void updateProgressNotification(int progress) 
        {
		    String deviceAddress = _deviceAddress;
		    String deviceName = _deviceName != null ? _deviceName : getString(R.string.dfu_unknown_name);

		    // Bitmap largeIcon = BitmapFactory.decodeResource(getResources(), R.drawable.ic_stat_notify_dfu); <- this looks bad on Android 5

		    NotificationCompat.Builder builder = new NotificationCompat.Builder(this).setSmallIcon(android.R.drawable.stat_sys_upload).setOnlyAlertOnce(true);//.setLargeIcon(largeIcon);
		    // Android 5
		    builder.setColor(Color.GRAY);

		    switch (progress) {
			    case ProgressConnecting:
				    builder.setOngoing(true).setContentTitle(getString(R.string.dfu_status_connecting)).setContentText(getString(R.string.dfu_status_connecting_msg, deviceName)).setProgress(100, 0, true);
				    break;
			    case PROGRESS_STARTING:
				    builder.setOngoing(true).setContentTitle(getString(R.string.dfu_status_starting)).setContentText(getString(R.string.dfu_status_starting_msg, deviceName)).setProgress(100, 0, true);
				    break;
			    case PROGRESS_ENABLING_DFU_MODE:
				    builder.setOngoing(true).setContentTitle(getString(R.string.dfu_status_switching_to_dfu)).setContentText(getString(R.string.dfu_status_switching_to_dfu_msg, deviceName))
						    .setProgress(100, 0, true);
				    break;
			    case PROGRESS_VALIDATING:
				    builder.setOngoing(true).setContentTitle(getString(R.string.dfu_status_validating)).setContentText(getString(R.string.dfu_status_validating_msg, deviceName)).setProgress(100, 0, true);
				    break;
			    case ProgressDisconnecting:
				    builder.setOngoing(true).setContentTitle(getString(R.string.dfu_status_disconnecting)).setContentText(getString(R.string.dfu_status_disconnecting_msg, deviceName))
						    .setProgress(100, 0, true);
				    break;
			    case ProgressCompleted:
				    builder.setOngoing(false).setContentTitle(getString(R.string.dfu_status_completed)).setSmallIcon(android.R.drawable.stat_sys_upload_done)
						    .setContentText(getString(R.string.dfu_status_completed_msg)).setAutoCancel(true).setColor(0xFF00B81A);
				    break;
			    case ProgressAborted:
				    builder.setOngoing(false).setContentTitle(getString(R.string.dfu_status_aborted)).setSmallIcon(android.R.drawable.stat_sys_upload_done)
						    .setContentText(getString(R.string.dfu_status_aborted_msg)).setAutoCancel(true);
				    break;
			    default:
				    if (progress >= ERROR_MASK) {
					    // progress is an error number
					    builder.setOngoing(false).setContentTitle(getString(R.string.dfu_status_error)).setSmallIcon(android.R.drawable.stat_sys_upload_done)
							    .setContentText(getString(R.string.dfu_status_error_msg)).setAutoCancel(true).setColor(Color.RED);
				    } else {
					    // progress is in percents
					    String title = _partsTotal == 1 ? getString(R.string.dfu_status_uploading) : getString(R.string.dfu_status_uploading_part, _partCurrent, _partsTotal);
					    String text = (_fileType & TypeApplication) > 0 ? getString(R.string.dfu_status_uploading_msg, deviceName) : getString(R.string.dfu_status_uploading_components_msg, deviceName);
					    builder.setOngoing(true).setContentTitle(title).setContentText(text).setProgress(100, progress, false);
				    }
		    }
		    // send progress or error broadcast
		    if (progress < ERROR_MASK)
			    sendProgressBroadcast(progress);
		    else
			    sendErrorBroadcast(progress);

		    // update the notification
		    Intent intent = new Intent(this, getNotificationTarget());
		    intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
		    intent.putExtra(EXTRA_DEVICE_ADDRESS, deviceAddress);
		    intent.putExtra(EXTRA_DEVICE_NAME, deviceName);
		    intent.putExtra(EXTRA_PROGRESS, progress); // this may contains ErrorConnectionMask bit!
		    PendingIntent pendingIntent = PendingIntent.getActivity(this, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT);
		    builder.setContentIntent(pendingIntent);

		    // Add Abort action to the notification
		    if (progress != ProgressAborted && progress != ProgressCompleted && progress < ERROR_MASK) {
			    Intent abortIntent = new Intent(BROADCAST_ACTION);
			    abortIntent.putExtra(EXTRA_ACTION, ACTION_ABORT);
			    PendingIntent pendingAbortIntent = PendingIntent.getBroadcast(this, 1, abortIntent, PendingIntent.FLAG_UPDATE_CURRENT);
			    builder.addAction(R.drawable.ic_action_notify_cancel, getString(R.string.dfu_action_abort), pendingAbortIntent);
		    }

		    NotificationManager manager = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
		    manager.notify(NOTIFICATION_ID, builder.build());
	    }

	    /**
	     * This method must return the activity class that will be used to create the pending intent used as a content intent in the notification showing the upload progress.
	     * The activity will be launched when user click the notification. DfuService will add {@link android.content.Intent#FLAG_ACTIVITY_NEW_TASK} flag and the following extras:
	     * <ul>
	     * <li>{@link #EXTRA_DEVICE_ADDRESS} - target device address</li>
	     * <li>{@link #EXTRA_DEVICE_NAME} - target device name</li>
	     * <li>{@link #EXTRA_PROGRESS} - the connection state (values < 0)*, current progress (0-100) or error number if {@link #ERROR_MASK} bit set.</li>
	     * </ul>
	     * <p>
	     * __________<br />
	     * * - connection state constants:
	     * <ul>
	     * <li>{@link #ProgressConnecting}</li>
	     * <li>{@link #ProgressDisconnecting}</li>
	     * <li>{@link #ProgressCompleted}</li>
	     * <li>{@link #ProgressAborted}</li>
	     * <li>{@link #PROGRESS_STARTING}</li>
	     * <li>{@link #PROGRESS_ENABLING_DFU_MODE}</li>
	     * <li>{@link #PROGRESS_VALIDATING}</li>
	     * </ul>
	     * </p>
	     *
	     * @return the target activity class
	     */
	    //protected abstract Class<? extends Activity> getNotificationTarget();

	    private void sendProgressBroadcast(int progress) 
        {
		    long now = SystemClock.elapsedRealtime();
		    float speed = now - _lastProgressTime != 0 ? (float) (_bytesSent - mLastBytesSent) / (float) (now - _lastProgressTime) : 0.0f;
		    float avgSpeed = now - mStartTime != 0 ? (float) _bytesSent / (float) (now - mStartTime) : 0.0f;
		    _lastProgressTime = now;
		    mLastBytesSent = _bytesSent;

		    Intent broadcast = new Intent(BROADCAST_PROGRESS);
		    broadcast.putExtra(EXTRA_DATA, progress);
		    broadcast.putExtra(EXTRA_DEVICE_ADDRESS, _deviceAddress);
		    broadcast.putExtra(EXTRA_PART_CURRENT, _partCurrent);
		    broadcast.putExtra(EXTRA_PARTS_TOTAL, _partsTotal);
		    broadcast.putExtra(EXTRA_SPEED_B_PER_MS, speed);
		    broadcast.putExtra(EXTRA_AVG_SPEED_B_PER_MS, avgSpeed);
		    LocalBroadcastManager.getInstance(this).sendBroadcast(broadcast);
	    }

	    private void sendErrorBroadcast(int error) 
        {
		    Intent broadcast = new Intent(BROADCAST_ERROR);
		    if ((error & ErrorConnectionMask) > 0) {
			    broadcast.putExtra(EXTRA_DATA, error & ~ErrorConnectionMask);
			    broadcast.putExtra(EXTRA_ERROR_TYPE, ERROR_TYPE_COMMUNICATION);
		    } else if ((error & ErrorConnectionStateMask) > 0) {
			    broadcast.putExtra(EXTRA_DATA, error & ~ErrorConnectionStateMask);
			    broadcast.putExtra(EXTRA_ERROR_TYPE, ERROR_TYPE_COMMUNICATION_STATE);
		    } else if ((error & ERROR_REMOTE_MASK) > 0) {
			    broadcast.putExtra(EXTRA_DATA, error);
			    broadcast.putExtra(EXTRA_ERROR_TYPE, ERROR_TYPE_DFU_REMOTE);
		    } else {
			    broadcast.putExtra(EXTRA_DATA, error);
			    broadcast.putExtra(EXTRA_ERROR_TYPE, ERROR_TYPE_OTHER);
		    }
		    broadcast.putExtra(EXTRA_DEVICE_ADDRESS, _deviceAddress);
		    LocalBroadcastManager.getInstance(this).sendBroadcast(broadcast);
	    }

	    private void sendLogBroadcast(int level, string message) 
        {
		    string fullMessage = "[DFU] " + message;
		    Intent broadcast = new Intent(BROADCAST_LOG);
		    broadcast.putExtra(EXTRA_LOG_MESSAGE, fullMessage);
		    broadcast.putExtra(EXTRA_LOG_LEVEL, level);
		    broadcast.putExtra(EXTRA_DEVICE_ADDRESS, _deviceAddress);
		    LocalBroadcastManager.getInstance(this).sendBroadcast(broadcast);
	    }

	    /**
	     * Initializes bluetooth adapter
	     *
	     * @return <code>true</code> if initialization was successful
	     */
	    private bool initialize() 
        {
		    // For API level 18 and above, get a reference to BluetoothAdapter through
		    // BluetoothManager.
		    BluetoothManager bluetoothManager = (BluetoothManager) getSystemService(Context.BLUETOOTH_SERVICE);
		    if (bluetoothManager == null) {
			    Log.Error("DfuBaseService", "Unable to initialize BluetoothManager.");
			    return false;
		    }

		    _bluetoothAdapter = bluetoothManager.GetAdapter();
		    if (_bluetoothAdapter == null) {
			    Log.Error("DfuBaseService", "Unable to obtain a BluetoothAdapter.");
			    return false;
		    }

		    return true;
	    }

	    private string parse(byte[] data) 
        {
		    if (data == null)
			    return "";

		    var length = data.Length;
		    if (length == 0)
			    return "";

		    char[] charOut = new char[length * 3 - 1];
		    for (int j = 0; j < length; j++) {
			    int v = data[j] & 0xFF;
			    charOut[j * 3] = _hexArray[(int)((uint)v >> 4)];
			    charOut[j * 3 + 1] = _hexArray[v & 0x0F];
			    if (j != length - 1)
				    charOut[j * 3 + 2] = '-';
		    }
		    return new string(charOut);
	    }

    }
}